/*
    Digital Evidence Management System
    SQL Server schema for cases, evidence, chain-of-custody, users/roles, and audit logging.

    Run against the target database, for example:
    sqlcmd -S "(localdb)\MSSQLLocalDB" -d DigitalEvidenceLab -i Sql\01_create_schema.sql
*/

IF OBJECT_ID('dbo.UserRoles', 'U') IS NOT NULL DROP TABLE dbo.UserRoles;
IF OBJECT_ID('dbo.AccessAuditLogs', 'U') IS NOT NULL DROP TABLE dbo.AccessAuditLogs;
IF OBJECT_ID('dbo.ChainOfCustody', 'U') IS NOT NULL DROP TABLE dbo.ChainOfCustody;
IF OBJECT_ID('dbo.Evidence', 'U') IS NOT NULL DROP TABLE dbo.Evidence;
IF OBJECT_ID('dbo.Cases', 'U') IS NOT NULL DROP TABLE dbo.Cases;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.Users
(
    UserId            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username          NVARCHAR(128) NOT NULL,
    DisplayName       NVARCHAR(160) NOT NULL,
    Email             NVARCHAR(256) NULL,
    PasswordHash      VARBINARY(256) NULL,
    IsActive          BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAtUtc      DATETIME2(3) NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    LastLoginAtUtc    DATETIME2(3) NULL,
    CONSTRAINT UQ_Users_Username UNIQUE (Username)
);
GO

CREATE TABLE dbo.Roles
(
    RoleId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    RoleName     NVARCHAR(64) NOT NULL,
    Description  NVARCHAR(256) NULL,
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

CREATE TABLE dbo.UserRoles
(
    UserId  INT NOT NULL,
    RoleId  INT NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);
GO

CREATE TABLE dbo.Cases
(
    CaseId              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cases PRIMARY KEY,
    CaseNumber          NVARCHAR(50) NOT NULL,
    Title               NVARCHAR(200) NOT NULL,
    Description         NVARCHAR(2000) NULL,
    Status              NVARCHAR(30) NOT NULL CONSTRAINT DF_Cases_Status DEFAULT ('Open'),
    Priority            NVARCHAR(30) NOT NULL CONSTRAINT DF_Cases_Priority DEFAULT ('Normal'),
    AssignedExaminerId  INT NULL,
    CreatedAtUtc        DATETIME2(3) NOT NULL CONSTRAINT DF_Cases_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc        DATETIME2(3) NOT NULL CONSTRAINT DF_Cases_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
    ClosedAtUtc         DATETIME2(3) NULL,
    CONSTRAINT UQ_Cases_CaseNumber UNIQUE (CaseNumber),
    CONSTRAINT CK_Cases_Status CHECK (Status IN ('Open', 'In Review', 'On Hold', 'Closed')),
    CONSTRAINT CK_Cases_Priority CHECK (Priority IN ('Low', 'Normal', 'High', 'Critical')),
    CONSTRAINT FK_Cases_AssignedExaminer FOREIGN KEY (AssignedExaminerId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.Evidence
(
    EvidenceId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Evidence PRIMARY KEY,
    CaseId              INT NOT NULL,
    UploadedByUserId    INT NOT NULL,
    OriginalFileName    NVARCHAR(260) NOT NULL,
    StoredFileName      NVARCHAR(260) NOT NULL,
    StoragePath         NVARCHAR(1024) NOT NULL,
    ContentType         NVARCHAR(150) NULL,
    EvidenceType        NVARCHAR(80) NOT NULL,
    FileSizeBytes       BIGINT NOT NULL,
    Sha256Hash          CHAR(64) NOT NULL,
    IsOriginalMedia     BIT NOT NULL CONSTRAINT DF_Evidence_IsOriginalMedia DEFAULT (0),
    UploadedAtUtc       DATETIME2(3) NOT NULL CONSTRAINT DF_Evidence_UploadedAtUtc DEFAULT (SYSUTCDATETIME()),
    LastVerifiedAtUtc   DATETIME2(3) NULL,
    VerificationStatus  NVARCHAR(30) NOT NULL CONSTRAINT DF_Evidence_VerificationStatus DEFAULT ('Pending'),
    Notes               NVARCHAR(2000) NULL,
    RowVersion          ROWVERSION NOT NULL,
    CONSTRAINT FK_Evidence_Cases FOREIGN KEY (CaseId) REFERENCES dbo.Cases(CaseId),
    CONSTRAINT FK_Evidence_UploadedBy FOREIGN KEY (UploadedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Evidence_FileSizeBytes CHECK (FileSizeBytes >= 0),
    CONSTRAINT CK_Evidence_Sha256Hash CHECK (Sha256Hash NOT LIKE '%[^0-9A-Fa-f]%'),
    CONSTRAINT CK_Evidence_VerificationStatus CHECK (VerificationStatus IN ('Pending', 'Verified', 'Mismatch', 'Missing'))
);
GO

CREATE TABLE dbo.ChainOfCustody
(
    CocId              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChainOfCustody PRIMARY KEY,
    EvidenceId         BIGINT NOT NULL,
    CaseId             INT NOT NULL,
    ActionType         NVARCHAR(60) NOT NULL,
    PerformedByUserId  INT NOT NULL,
    PerformedAtUtc     DATETIME2(3) NOT NULL CONSTRAINT DF_ChainOfCustody_PerformedAtUtc DEFAULT (SYSUTCDATETIME()),
    Location           NVARCHAR(200) NOT NULL,
    NarrativeNote      NVARCHAR(2000) NOT NULL,
    FromCustodianId    INT NULL,
    ToCustodianId      INT NULL,
    FileHashAtAction   CHAR(64) NULL,
    SourceIpAddress    NVARCHAR(64) NULL,
    CONSTRAINT FK_CoC_Evidence FOREIGN KEY (EvidenceId) REFERENCES dbo.Evidence(EvidenceId),
    CONSTRAINT FK_CoC_Cases FOREIGN KEY (CaseId) REFERENCES dbo.Cases(CaseId),
    CONSTRAINT FK_CoC_PerformedBy FOREIGN KEY (PerformedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_CoC_FromCustodian FOREIGN KEY (FromCustodianId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_CoC_ToCustodian FOREIGN KEY (ToCustodianId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_CoC_ActionType CHECK (ActionType IN ('upload', 'imaging', 'transfer', 'export', 'download', 'hash_verify', 'metadata_update', 'access'))
);
GO

CREATE TABLE dbo.AccessAuditLogs
(
    AuditLogId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccessAuditLogs PRIMARY KEY,
    UserId          INT NULL,
    Username        NVARCHAR(128) NOT NULL,
    EventType       NVARCHAR(60) NOT NULL,
    EntityType      NVARCHAR(60) NULL,
    EntityId        NVARCHAR(80) NULL,
    OccurredAtUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_AccessAuditLogs_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
    IpAddress       NVARCHAR(64) NULL,
    UserAgent       NVARCHAR(512) NULL,
    Details         NVARCHAR(2000) NULL,
    Succeeded       BIT NOT NULL CONSTRAINT DF_AccessAuditLogs_Succeeded DEFAULT (1),
    CONSTRAINT FK_Audit_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Audit_EventType CHECK (EventType IN ('login', 'logout', 'case_view', 'case_create', 'upload', 'download', 'hash_verify', 'metadata_change', 'access_denied'))
);
GO

CREATE INDEX IX_Cases_StatusPriority ON dbo.Cases(Status, Priority);
CREATE INDEX IX_Evidence_CaseId ON dbo.Evidence(CaseId);
CREATE INDEX IX_Evidence_Sha256Hash ON dbo.Evidence(Sha256Hash);
CREATE INDEX IX_CoC_EvidenceTime ON dbo.ChainOfCustody(EvidenceId, PerformedAtUtc DESC);
CREATE INDEX IX_Audit_UserTime ON dbo.AccessAuditLogs(UserId, OccurredAtUtc DESC);
CREATE INDEX IX_Audit_EntityTime ON dbo.AccessAuditLogs(EntityType, EntityId, OccurredAtUtc DESC);
GO

INSERT INTO dbo.Roles (RoleName, Description)
VALUES
    ('Administrator', 'Can manage users, cases, evidence, and system settings.'),
    ('Examiner', 'Can create cases, upload evidence, and add chain-of-custody entries.'),
    ('Reviewer', 'Can review case and evidence records.'),
    ('ReadOnly', 'Can view permitted case records without modifying evidence.');

INSERT INTO dbo.Users (Username, DisplayName, Email, PasswordHash)
VALUES
    ('LAB\admin', 'Demo Administrator', 'admin@example.local', 0x01000186A06663B8433D97E57664AB2909C92F67600FB243553225D21490FB10A571F121F9721CEF688DA72049EF24C2CC9F9689C4),
    ('LAB\examiner', 'Demo Examiner', 'examiner@example.local', 0x01000186A011A7F037FED27B7237078868090C4A7533B4E3780D1A3AA4102B69FD2F8DF8CCA7BE39F5E1D287D148F8527B0C2859F8),
    ('LAB\reviewer', 'Demo Reviewer', 'reviewer@example.local', 0x01000186A0AE185E6991EC82AF0251D5DC3571FA428F6F5777EE8779D0D2F58540DEF9CFFC8C60B55F92F6E2730ECB70D6769F5D10),
    ('LAB\readonly', 'Demo Read Only', 'readonly@example.local', 0x01000186A092BEA02E8879614309E679113FC254C4DC4B33692CC040B2B34E9E6B6102C8C3BF8237804F6B3A1F748F1B824851F5DC);

INSERT INTO dbo.UserRoles (UserId, RoleId)
SELECT u.UserId, r.RoleId
FROM dbo.Users u
JOIN dbo.Roles r ON
    (u.Username = 'LAB\admin' AND r.RoleName = 'Administrator')
    OR (u.Username = 'LAB\examiner' AND r.RoleName = 'Examiner')
    OR (u.Username = 'LAB\reviewer' AND r.RoleName = 'Reviewer')
    OR (u.Username = 'LAB\readonly' AND r.RoleName = 'ReadOnly');

INSERT INTO dbo.Cases (CaseNumber, Title, Description, Status, Priority, AssignedExaminerId)
SELECT 'DF-2026-0001',
       'Sample Unauthorized Access Investigation',
       'Seed case for validating evidence upload, hashing, chain-of-custody, and audit workflows.',
       'Open',
       'High',
       u.UserId
FROM dbo.Users u
WHERE u.Username = 'LAB\examiner';
GO
