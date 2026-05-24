using System.Security.Claims;
using DigitalEvidenceManagementSystem.Models;

namespace DigitalEvidenceManagementSystem.Services;

public sealed class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<AppUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var principal = httpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("No authenticated user is available for this request.");
        }

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            throw new InvalidOperationException("Authenticated user is missing a valid user id claim.");
        }

        var username = principal.Identity.Name ?? string.Empty;
        var displayName = principal.FindFirstValue(ClaimTypes.GivenName) ?? username;
        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        return Task.FromResult(new AppUser
        {
            UserId = userId,
            Username = username,
            DisplayName = displayName,
            Email = principal.FindFirstValue(ClaimTypes.Email),
            IsActive = true,
            Roles = roles
        });
    }
}
