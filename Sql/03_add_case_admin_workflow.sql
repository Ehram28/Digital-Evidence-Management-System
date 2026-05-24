/*
    Adds admin case editing, completion/cancellation reasons, and case removal
    audit event types without dropping existing data.

    Run against an existing DigitalEvidenceLab database:
    sqlcmd -S "(localdb)\MSSQLLocalDB" -d DigitalEvidenceLab -i Sql\03_add_case_admin_workflow.sql
*/

IF COL_LENGTH('dbo.Cases', 'ClosedByUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Cases ADD ClosedByUserId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Cases', 'ResolutionReason') IS NULL
BEGIN
    ALTER TABLE dbo.Cases ADD ResolutionReason NVARCHAR(2000) NULL;
END;
GO

IF OBJECT_ID('dbo.FK_Cases_ClosedBy', 'F') IS NULL
BEGIN
    ALTER TABLE dbo.Cases
    ADD CONSTRAINT FK_Cases_ClosedBy FOREIGN KEY (ClosedByUserId) REFERENCES dbo.Users(UserId);
END;
GO

IF OBJECT_ID('dbo.CK_Cases_Status', 'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Cases DROP CONSTRAINT CK_Cases_Status;
END;
GO

ALTER TABLE dbo.Cases
ADD CONSTRAINT CK_Cases_Status CHECK (Status IN ('Open', 'In Review', 'On Hold', 'Completed', 'Cancelled', 'Closed'));
GO

IF OBJECT_ID('dbo.CK_Audit_EventType', 'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.AccessAuditLogs DROP CONSTRAINT CK_Audit_EventType;
END;
GO

ALTER TABLE dbo.AccessAuditLogs
ADD CONSTRAINT CK_Audit_EventType CHECK (EventType IN ('login', 'logout', 'case_view', 'case_create', 'case_update', 'case_complete', 'case_cancel', 'case_delete', 'upload', 'download', 'hash_verify', 'metadata_change', 'access_denied'));
GO
