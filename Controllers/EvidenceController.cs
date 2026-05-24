using DigitalEvidenceManagementSystem.Data;
using DigitalEvidenceManagementSystem.Models;
using DigitalEvidenceManagementSystem.Models.ViewModels;
using DigitalEvidenceManagementSystem.Security;
using DigitalEvidenceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DigitalEvidenceManagementSystem.Controllers;

[Authorize(Roles = AppRoles.AnyLabUser)]
public sealed class EvidenceController : Controller
{
    private readonly CaseRepository _caseRepository;
    private readonly EvidenceRepository _evidenceRepository;
    private readonly CurrentUserService _currentUserService;
    private readonly FileHashService _fileHashService;
    private readonly AuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;
    private readonly EvidenceStorageOptions _storageOptions;

    public EvidenceController(
        CaseRepository caseRepository,
        EvidenceRepository evidenceRepository,
        CurrentUserService currentUserService,
        FileHashService fileHashService,
        AuditLogService auditLogService,
        IWebHostEnvironment environment,
        IOptions<EvidenceStorageOptions> storageOptions)
    {
        _caseRepository = caseRepository;
        _evidenceRepository = evidenceRepository;
        _currentUserService = currentUserService;
        _fileHashService = fileHashService;
        _auditLogService = auditLogService;
        _environment = environment;
        _storageOptions = storageOptions.Value;
    }

    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var evidence = await _evidenceRepository.GetByIdAsync(id, cancellationToken);
        if (evidence is null)
        {
            return NotFound();
        }

        var entries = await _evidenceRepository.GetCustodyEntriesAsync(id, cancellationToken);
        return View(new EvidenceDetailsViewModel
        {
            Evidence = evidence,
            CustodyEntries = entries,
            StatusMessage = TempData["StatusMessage"] as string
        });
    }

    [Authorize(Roles = AppRoles.EvidenceWriters)]
    public async Task<IActionResult> Upload(int caseId, CancellationToken cancellationToken)
    {
        var caseRecord = await _caseRepository.GetByIdAsync(caseId, cancellationToken);
        if (caseRecord is null)
        {
            return NotFound();
        }

        if (caseRecord.IsTerminal)
        {
            return Conflict("Evidence cannot be uploaded to a completed or cancelled case.");
        }

        return View(new EvidenceUploadViewModel
        {
            CaseId = caseId,
            CaseNumber = caseRecord.CaseNumber
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(long.MaxValue)]
    [Authorize(Roles = AppRoles.EvidenceWriters)]
    public async Task<IActionResult> Upload(EvidenceUploadViewModel model, CancellationToken cancellationToken)
    {
        var caseRecord = await _caseRepository.GetByIdAsync(model.CaseId, cancellationToken);
        if (caseRecord is null)
        {
            return NotFound();
        }

        if (caseRecord.IsTerminal)
        {
            ModelState.AddModelError(string.Empty, "Evidence cannot be uploaded to a completed or cancelled case.");
        }

        model.CaseNumber = caseRecord.CaseNumber;
        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "Select a non-empty evidence file.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var originalFileName = Path.GetFileName(model.File!.FileName);
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(SanitizePathSegment(caseRecord.CaseNumber), storedFileName);
        var fullPath = GetEvidenceFullPath(relativePath);

        await using var uploadStream = model.File.OpenReadStream();
        var sha256Hash = await _fileHashService.SaveAndHashAsync(uploadStream, fullPath, cancellationToken);

        var evidence = new EvidenceItem
        {
            CaseId = model.CaseId,
            UploadedByUserId = user.UserId,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            StoragePath = relativePath,
            ContentType = model.File.ContentType,
            EvidenceType = model.EvidenceType,
            FileSizeBytes = model.File.Length,
            Sha256Hash = sha256Hash,
            IsOriginalMedia = model.IsOriginalMedia,
            Notes = model.Notes
        };

        var custodyEntry = new ChainOfCustodyEntry
        {
            CaseId = model.CaseId,
            ActionType = "upload",
            PerformedByUserId = user.UserId,
            Location = model.Location,
            NarrativeNote = model.NarrativeNote,
            FileHashAtAction = sha256Hash,
            SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var evidenceId = await _evidenceRepository.CreateEvidenceWithCustodyAsync(evidence, custodyEntry, cancellationToken);
        await _auditLogService.LogAsync(user, "upload", "evidence", evidenceId.ToString(), $"Uploaded {originalFileName}; SHA-256 {sha256Hash}.", cancellationToken: cancellationToken);

        return RedirectToAction(nameof(Details), new { id = evidenceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.EvidenceReviewers)]
    public async Task<IActionResult> Verify(long id, CancellationToken cancellationToken)
    {
        var evidence = await _evidenceRepository.GetByIdAsync(id, cancellationToken);
        if (evidence is null)
        {
            return NotFound();
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var fullPath = GetEvidenceFullPath(evidence.StoragePath);
        var exists = System.IO.File.Exists(fullPath);
        var currentHash = exists ? await _fileHashService.ComputeSha256Async(fullPath, cancellationToken) : null;
        var matched = string.Equals(currentHash, evidence.Sha256Hash, StringComparison.OrdinalIgnoreCase);
        var status = exists ? matched ? "Verified" : "Mismatch" : "Missing";

        await _evidenceRepository.UpdateVerificationStatusAsync(id, status, cancellationToken);
        await _evidenceRepository.RecordCustodyEntryAsync(new ChainOfCustodyEntry
        {
            EvidenceId = evidence.EvidenceId,
            CaseId = evidence.CaseId,
            ActionType = "hash_verify",
            PerformedByUserId = user.UserId,
            Location = "Application hash verification",
            NarrativeNote = matched
                ? "SHA-256 re-check matched the stored evidence hash."
                : $"SHA-256 re-check failed. Expected {evidence.Sha256Hash}; actual {(currentHash ?? "file missing")}.",
            FileHashAtAction = currentHash,
            SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, cancellationToken);
        await _auditLogService.LogAsync(user, "hash_verify", "evidence", id.ToString(), $"Hash verification result: {status}.", status == "Verified", cancellationToken);

        TempData["StatusMessage"] = status == "Verified"
            ? "Hash verification passed."
            : $"Hash verification failed: {status}.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = AppRoles.EvidenceReviewers)]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        var evidence = await _evidenceRepository.GetByIdAsync(id, cancellationToken);
        if (evidence is null)
        {
            return NotFound();
        }

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var fullPath = GetEvidenceFullPath(evidence.StoragePath);
        if (!System.IO.File.Exists(fullPath))
        {
            await _evidenceRepository.UpdateVerificationStatusAsync(id, "Missing", cancellationToken);
            await _auditLogService.LogAsync(user, "download", "evidence", id.ToString(), "Download blocked because the stored evidence file is missing.", false, cancellationToken);
            return Conflict("Stored evidence file is missing. Download was blocked and audit logged.");
        }

        var currentHash = await _fileHashService.ComputeSha256Async(fullPath, cancellationToken);
        if (!string.Equals(currentHash, evidence.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            await _evidenceRepository.UpdateVerificationStatusAsync(id, "Mismatch", cancellationToken);
            await _evidenceRepository.RecordCustodyEntryAsync(new ChainOfCustodyEntry
            {
                EvidenceId = evidence.EvidenceId,
                CaseId = evidence.CaseId,
                ActionType = "hash_verify",
                PerformedByUserId = user.UserId,
                Location = "Download hash gate",
                NarrativeNote = $"Download blocked because SHA-256 did not match. Expected {evidence.Sha256Hash}; actual {currentHash}.",
                FileHashAtAction = currentHash,
                SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            }, cancellationToken);
            await _auditLogService.LogAsync(user, "download", "evidence", id.ToString(), "Download blocked because SHA-256 verification failed.", false, cancellationToken);
            return Conflict("SHA-256 verification failed. Download was blocked and audit logged.");
        }

        await _evidenceRepository.UpdateVerificationStatusAsync(id, "Verified", cancellationToken);
        await _evidenceRepository.RecordCustodyEntryAsync(new ChainOfCustodyEntry
        {
            EvidenceId = evidence.EvidenceId,
            CaseId = evidence.CaseId,
            ActionType = "download",
            PerformedByUserId = user.UserId,
            Location = "Evidence download",
            NarrativeNote = "Evidence file downloaded after successful SHA-256 verification.",
            FileHashAtAction = currentHash,
            SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, cancellationToken);
        await _auditLogService.LogAsync(user, "download", "evidence", id.ToString(), $"Downloaded after SHA-256 verification: {currentHash}.", cancellationToken: cancellationToken);

        return PhysicalFile(fullPath, evidence.ContentType ?? "application/octet-stream", evidence.OriginalFileName);
    }

    private string GetEvidenceFullPath(string relativePath)
    {
        var root = Path.IsPathRooted(_storageOptions.RootPath)
            ? _storageOptions.RootPath
            : Path.Combine(_environment.ContentRootPath, _storageOptions.RootPath);

        var rootFullPath = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Evidence storage path escaped the configured storage root.");
        }

        return fullPath;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "case" : sanitized;
    }
}
