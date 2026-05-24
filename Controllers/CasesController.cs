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
        if (!CaseCreateViewModel.Statuses.Contains(model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "Select a valid active status.");
        }

        if (!CaseCreateViewModel.Priorities.Contains(model.Priority))
        {
            ModelState.AddModelError(nameof(model.Priority), "Select a valid priority.");
        }

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

    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var caseRecord = await _caseRepository.GetByIdAsync(id, cancellationToken);
        if (caseRecord is null)
        {
            return NotFound();
        }

        return View(new CaseEditViewModel
        {
            CaseId = caseRecord.CaseId,
            CaseNumber = caseRecord.CaseNumber,
            Title = caseRecord.Title,
            Description = caseRecord.Description,
            Priority = caseRecord.Priority
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> Edit(CaseEditViewModel model, CancellationToken cancellationToken)
    {
        if (!CaseEditViewModel.Priorities.Contains(model.Priority))
        {
            ModelState.AddModelError(nameof(model.Priority), "Select a valid priority.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        try
        {
            await _caseRepository.UpdateAsync(model, cancellationToken);
            await _auditLogService.LogAsync(user, "case_update", "case", model.CaseId.ToString(), $"Updated case {model.CaseNumber}.", cancellationToken: cancellationToken);
            TempData["StatusMessage"] = "Case details were updated.";
            return RedirectToAction(nameof(Details), new { id = model.CaseId });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            ModelState.AddModelError(nameof(model.CaseNumber), "A case with this ID already exists.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public Task<IActionResult> Complete(int id, string? reason, CancellationToken cancellationToken)
    {
        return ResolveCaseAsync(id, "Completed", "case_complete", reason, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public Task<IActionResult> Cancel(int id, string? reason, CancellationToken cancellationToken)
    {
        return ResolveCaseAsync(id, "Cancelled", "case_cancel", reason, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> Delete(int id, string? reason, CancellationToken cancellationToken)
    {
        var caseRecord = await _caseRepository.GetByIdAsync(id, cancellationToken);
        if (caseRecord is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["StatusMessage"] = "Removal requires a reason.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var deleted = await _caseRepository.DeleteIfEmptyAsync(id, cancellationToken);
        if (!deleted)
        {
            await _auditLogService.LogAsync(user, "case_delete", "case", id.ToString(), $"Removal blocked for {caseRecord.CaseNumber}. Reason entered: {reason.Trim()}.", false, cancellationToken);
            TempData["StatusMessage"] = "This case has evidence and cannot be removed. Cancel it with a reason instead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _auditLogService.LogAsync(user, "case_delete", "case", id.ToString(), $"Removed case {caseRecord.CaseNumber}. Reason: {reason.Trim()}.", cancellationToken: cancellationToken);
        TempData["StatusMessage"] = $"Case {caseRecord.CaseNumber} was removed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ResolveCaseAsync(int id, string status, string eventType, string? reason, CancellationToken cancellationToken)
    {
        var caseRecord = await _caseRepository.GetByIdAsync(id, cancellationToken);
        if (caseRecord is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["StatusMessage"] = $"{status} requires a reason.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        await _caseRepository.ResolveAsync(id, status, reason, user.UserId, cancellationToken);
        await _auditLogService.LogAsync(user, eventType, "case", id.ToString(), $"{status} case {caseRecord.CaseNumber}. Reason: {reason.Trim()}.", cancellationToken: cancellationToken);

        TempData["StatusMessage"] = $"Case marked {status.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
