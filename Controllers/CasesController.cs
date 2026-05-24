using DigitalEvidenceManagementSystem.Data;
using DigitalEvidenceManagementSystem.Models.ViewModels;
using DigitalEvidenceManagementSystem.Security;
using DigitalEvidenceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DigitalEvidenceManagementSystem.Controllers;

[Authorize(Roles = AppRoles.AnyLabUser)]
public sealed class CasesController : Controller
{
    private readonly CaseRepository _caseRepository;
    private readonly EvidenceRepository _evidenceRepository;
    private readonly CurrentUserService _currentUserService;
    private readonly AuditLogService _auditLogService;

    public CasesController(
        CaseRepository caseRepository,
        EvidenceRepository evidenceRepository,
        CurrentUserService currentUserService,
        AuditLogService auditLogService)
    {
        _caseRepository = caseRepository;
        _evidenceRepository = evidenceRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, string? status, string? priority, CancellationToken cancellationToken)
    {
        var cases = await _caseRepository.SearchAsync(search, status, priority, cancellationToken: cancellationToken);
        return View(new CaseListViewModel
        {
            Search = search,
            Status = status,
            Priority = priority,
            Cases = cases
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var caseRecord = await _caseRepository.GetByIdAsync(id, cancellationToken);
        if (caseRecord is null)
        {
            return NotFound();
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        await _auditLogService.LogAsync(user, "case_view", "case", id.ToString(), $"Viewed case {caseRecord.CaseNumber}.", cancellationToken: cancellationToken);

        var evidence = await _evidenceRepository.GetByCaseAsync(id, cancellationToken);
        return View(new CaseDetailsViewModel
        {
            Case = caseRecord,
            EvidenceItems = evidence
        });
    }

    [Authorize(Roles = AppRoles.CaseWriters)]
    public IActionResult Create()
    {
        return View(new CaseCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.CaseWriters)]
    public async Task<IActionResult> Create(CaseCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        try
        {
            var caseId = await _caseRepository.CreateAsync(model, user.UserId, cancellationToken);
            await _auditLogService.LogAsync(user, "case_create", "case", caseId.ToString(), $"Created case {model.CaseNumber}.", cancellationToken: cancellationToken);
            return RedirectToAction(nameof(Details), new { id = caseId });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            ModelState.AddModelError(nameof(model.CaseNumber), "A case with this ID already exists.");
            return View(model);
        }
    }
}
