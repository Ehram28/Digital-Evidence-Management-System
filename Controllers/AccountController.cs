using System.Globalization;
using System.Security.Claims;
using DigitalEvidenceManagementSystem.Data;
using DigitalEvidenceManagementSystem.Models;
using DigitalEvidenceManagementSystem.Models.ViewModels;
using DigitalEvidenceManagementSystem.Security;
using DigitalEvidenceManagementSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DigitalEvidenceManagementSystem.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserRepository _userRepository;
    private readonly PasswordHashService _passwordHashService;
    private readonly AuditLogRepository _auditLogRepository;

    public AccountController(
        UserRepository userRepository,
        PasswordHashService passwordHashService,
        AuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _passwordHashService = passwordHashService;
        _auditLogRepository = auditLogRepository;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = model.Username.Trim();
        var login = await _userRepository.GetLoginByUsernameAsync(username, cancellationToken);
        var validLogin = login is not null
            && login.User.IsActive
            && login.User.Roles.Count > 0
            && _passwordHashService.VerifyPassword(login.PasswordHash, model.Password);

        if (!validLogin)
        {
            await WriteAuditLogAsync(
                login?.User.UserId,
                username,
                "login",
                null,
                null,
                "Login failed.",
                false,
                cancellationToken);

            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var user = login!.User;
        await SignInUserAsync(user, model.RememberMe);
        await _userRepository.RecordLoginAsync(user.UserId, cancellationToken);
        await WriteAuditLogAsync(user.UserId, user.Username, "login", null, null, "Login succeeded.", true, cancellationToken);

        return RedirectToLocal(model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = model.Username.Trim();
        var displayName = model.DisplayName.Trim();
        var email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        var role = model.Role.Trim();

        if (username.Length == 0)
        {
            ModelState.AddModelError(nameof(model.Username), "Username is required.");
            return View(model);
        }

        if (displayName.Length == 0)
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Display name is required.");
            return View(model);
        }

        if (!AppRoles.TryGetForensicTeamRole(role, out var selectedRole))
        {
            ModelState.AddModelError(nameof(model.Role), "Select a valid forensic team role.");
            return View(model);
        }

        try
        {
            var passwordHash = _passwordHashService.HashPassword(model.Password);
            var user = await _userRepository.CreateAsync(
                username,
                displayName,
                email,
                passwordHash,
                selectedRole!.Name,
                selectedRole.Description,
                cancellationToken);

            await SignInUserAsync(user, model.RememberMe);
            await _userRepository.RecordLoginAsync(user.UserId, cancellationToken);
            await WriteAuditLogAsync(user.UserId, user.Username, "login", null, null, $"Signup succeeded with {selectedRole.Name} role and user signed in.", true, cancellationToken);

            return RedirectToLocal(model.ReturnUrl);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            ModelState.AddModelError(nameof(model.Username), "An account with this username already exists.");
            return View(model);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "unknown";
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (int?)null;

        await WriteAuditLogAsync(userId, username, "logout", null, null, "Logout succeeded.", true, cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public async Task<IActionResult> AccessDenied(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.Name ?? "unknown";
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (int?)null;

        await WriteAuditLogAsync(
            userId,
            username,
            "access_denied",
            null,
            null,
            $"Access denied for {returnUrl ?? "requested resource"}.",
            false,
            cancellationToken);

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private Task SignInUserAsync(AppUser user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.DisplayName)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null
            });
    }

    private Task WriteAuditLogAsync(
        int? userId,
        string username,
        string eventType,
        string? entityType,
        string? entityId,
        string? details,
        bool succeeded,
        CancellationToken cancellationToken)
    {
        return _auditLogRepository.AddAsync(
            userId,
            username,
            eventType,
            entityType,
            entityId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            details,
            succeeded,
            cancellationToken);
    }
}
