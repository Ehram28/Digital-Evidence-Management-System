# Digital Evidence Management System

ASP.NET Core MVC + SQL Server scaffold for a digital-forensics lab case-management system. It favors clean evidence metadata, SHA-256 integrity checks, chain-of-custody entries, and audit logging over heavy UI features.

## Project Structure

```text
DigitalEvidenceManagementSystem/
  Controllers/
    AccountController.cs
    CasesController.cs
    EvidenceController.cs
    HomeController.cs
  Data/
    SqlConnectionFactory.cs
    UserRepository.cs
    CaseRepository.cs
    EvidenceRepository.cs
    AuditLogRepository.cs
  Models/
    AppUser.cs
    CaseRecord.cs
    EvidenceItem.cs
    ChainOfCustodyEntry.cs
    ViewModels/
  Services/
    CurrentUserService.cs
    FileHashService.cs
    AuditLogService.cs
    EvidenceStorageOptions.cs
  Sql/
    01_create_schema.sql
    02_add_auth_seed_passwords.sql
  Views/
    Account/
    Cases/
    Evidence/
    Home/
  wwwroot/css/site.css
```

## Database

Run `Sql/01_create_schema.sql` against SQL Server. The script creates:

- `Users`, `Roles`, `UserRoles`
- `Cases`
- `Evidence` with `Sha256Hash`
- `ChainOfCustody`
- `AccessAuditLogs`

Default connection string in `appsettings.json` targets LocalDB:

```json
"Server=(localdb)\\MSSQLLocalDB;Database=DigitalEvidenceLab;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

## Authentication Model

The app uses cookie authentication backed by the existing `Users`, `Roles`, and `UserRoles` tables. Passwords are stored as PBKDF2-SHA256 payloads in `Users.PasswordHash`, and successful logins create role claims for authorization.

Demo accounts seeded by `Sql/01_create_schema.sql` and `Sql/02_add_auth_seed_passwords.sql`:

- `LAB\admin` / `Admin123!` - Administrator
- `LAB\examiner` / `Examiner123!` - Examiner
- `LAB\reviewer` / `Reviewer123!` - Reviewer
- `LAB\readonly` / `ReadOnly123!` - ReadOnly

Users can also create an account from the sign-up page and choose their forensic team role. The selected role is saved to `UserRoles`, then issued as a cookie role claim so the app opens only the workflows required for that job.

Role access:

- Administrator: full access, including case edit, completion, cancellation with reason, and removal of empty cases.
- Investigator: create cases, upload evidence, verify hashes, and download evidence.
- Reviewer: view cases/evidence and verify hashes only; evidence download and case modification are not allowed.
- Reader: view dashboards, cases, and evidence metadata only.

Existing database upgrades can be applied with `Sql/03_add_case_admin_workflow.sql`. It adds case resolution reasons, terminal statuses (`Completed`, `Cancelled`), and audit event types for admin case actions.

## Evidence Upload and SHA-256

`EvidenceController.Upload` streams the uploaded file to `App_Data\EvidenceStore`, computes SHA-256 during the copy, inserts the evidence row, then inserts the first CoC row in the same SQL transaction:

```csharp
await using var uploadStream = model.File.OpenReadStream();
var sha256Hash = await _fileHashService.SaveAndHashAsync(uploadStream, fullPath, cancellationToken);

var evidenceId = await _evidenceRepository.CreateEvidenceWithCustodyAsync(evidence, custodyEntry, cancellationToken);
```

`FileHashService` uses a 1 MB buffer and `SHA256.TransformBlock` so large forensic files do not need to be loaded into memory.

## Hash Verification on Access

`EvidenceController.Download` recalculates SHA-256 before returning the physical file. If the hash does not match the stored value, the download is blocked, evidence status is changed to `Mismatch`, a CoC `hash_verify` entry is added, and the failed download is audit logged.

Manual re-checks are supported by the `Verify hash` button on the evidence details page.

## UI

- Dashboard: open case counts, high-priority counts, evidence totals, hash mismatch count, recent cases.
- Cases: filter by case ID/title, status, and priority.
- Case details: case metadata plus evidence list.
- Evidence upload: file, evidence type, original-media flag, location, notes, and required CoC narrative.
- Evidence details: integrity metadata, download/hash verify actions, and full CoC timeline.

## Forensic Integrity

- Every evidence file has a stored SHA-256 digest.
- Upload, manual verification, and download actions log the hash observed at the time of action.
- Downloads are gated by hash verification.
- Chain-of-custody records are append-only by workflow and include examiner, UTC timestamp, location, narrative, source IP, and hash at action.
- Audit logs capture case views, case creation, uploads, downloads, and hash verification attempts.
