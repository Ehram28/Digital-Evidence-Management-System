# Digital Evidence Management System

A role-based ASP.NET Core MVC application for managing digital-forensics cases, evidence metadata, SHA-256 integrity checks, chain-of-custody history, and audit activity.

This project is designed as a portfolio-ready case-management system for a forensic lab workflow. It demonstrates secure authentication, SQL Server persistence, role-based access control, evidence verification, and clean MVC separation.

## Highlights

- Case dashboard with search, status filters, priorities, and recent activity.
- Evidence upload workflow with SHA-256 hashing and chain-of-custody logging.
- Admin case controls for editing, completing, cancelling with a reason, and removing empty cases.
- Role-based permissions for Administrator, Investigator, Reviewer, and Reader users.
- Audit logging for sign-in, case activity, evidence upload, download, and hash verification.

## Tech Stack

- ASP.NET Core MVC on .NET 8
- SQL Server / LocalDB
- Cookie authentication with PBKDF2-SHA256 password hashes
- Bootstrap, Razor views, and custom CSS

## Roles

- Administrator: full case and evidence management.
- Investigator: creates cases, uploads evidence, verifies hashes, and downloads evidence.
- Reviewer: reviews records and verifies hashes only.
- Reader: read-only dashboard, case, and metadata access.

## Quick Start

1. Create the database:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -Q "CREATE DATABASE DigitalEvidenceLab"
sqlcmd -S "(localdb)\MSSQLLocalDB" -d DigitalEvidenceLab -i Sql\01_create_schema.sql
```

2. Run the app:

```powershell
dotnet run
```

3. Open:

```text
http://localhost:5191
```

Demo login:

```text
LAB\admin / Admin123!
```

## Documentation

The detailed technical reference has been moved to [Docs/PROJECT_REFERENCE.md](Docs/PROJECT_REFERENCE.md). It includes the full project structure, database notes, seeded accounts, authentication details, evidence hashing workflow, and forensic integrity notes.
