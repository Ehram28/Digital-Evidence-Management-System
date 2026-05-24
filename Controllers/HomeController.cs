using System.Diagnostics;
using DigitalEvidenceManagementSystem.Data;
using DigitalEvidenceManagementSystem.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DigitalEvidenceManagementSystem.Models;

namespace DigitalEvidenceManagementSystem.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly CaseRepository _caseRepository;

    public HomeController(ILogger<HomeController> logger, CaseRepository caseRepository)
    {
        _logger = logger;
        _caseRepository = caseRepository;
    }

    [Authorize(Roles = AppRoles.AnyLabUser)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await _caseRepository.GetDashboardAsync(cancellationToken);
        return View(dashboard);
    }

    [Authorize(Roles = AppRoles.AnyLabUser)]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
