/*
    Adds password-based demo accounts for the role-based login system without
    dropping existing case or evidence data.

    Run after Sql\01_create_schema.sql, or against an existing DigitalEvidenceLab database:
    sqlcmd -S "(localdb)\MSSQLLocalDB" -d DigitalEvidenceLab -i Sql\02_add_auth_seed_passwords.sql
*/

MERGE dbo.Roles AS target
USING (VALUES
    ('Administrator', 'Can manage users, cases, evidence, and system settings.'),
    ('Examiner', 'Investigator role: can create cases, upload evidence, and add chain-of-custody entries.'),
    ('Reviewer', 'Can review case and evidence records.'),
    ('ReadOnly', 'Can view permitted case records without modifying evidence.')
) AS source (RoleName, Description)
ON target.RoleName = source.RoleName
WHEN MATCHED THEN
    UPDATE SET Description = source.Description
WHEN NOT MATCHED THEN
    INSERT (RoleName, Description)
    VALUES (source.RoleName, source.Description);
GO

MERGE dbo.Users AS target
USING (VALUES
    ('LAB\admin', 'Demo Administrator', 'admin@example.local', 0x01000186A06663B8433D97E57664AB2909C92F67600FB243553225D21490FB10A571F121F9721CEF688DA72049EF24C2CC9F9689C4),
    ('LAB\examiner', 'Demo Examiner', 'examiner@example.local', 0x01000186A011A7F037FED27B7237078868090C4A7533B4E3780D1A3AA4102B69FD2F8DF8CCA7BE39F5E1D287D148F8527B0C2859F8),
    ('LAB\reviewer', 'Demo Reviewer', 'reviewer@example.local', 0x01000186A0AE185E6991EC82AF0251D5DC3571FA428F6F5777EE8779D0D2F58540DEF9CFFC8C60B55F92F6E2730ECB70D6769F5D10),
    ('LAB\readonly', 'Demo Read Only', 'readonly@example.local', 0x01000186A092BEA02E8879614309E679113FC254C4DC4B33692CC040B2B34E9E6B6102C8C3BF8237804F6B3A1F748F1B824851F5DC)
) AS source (Username, DisplayName, Email, PasswordHash)
ON target.Username = source.Username
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = source.DisplayName,
        Email = source.Email,
        PasswordHash = source.PasswordHash,
        IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (Username, DisplayName, Email, PasswordHash, IsActive)
    VALUES (source.Username, source.DisplayName, source.Email, source.PasswordHash, 1);
GO

INSERT INTO dbo.UserRoles (UserId, RoleId)
SELECT u.UserId, r.RoleId
FROM (VALUES
    ('LAB\admin', 'Administrator'),
    ('LAB\examiner', 'Examiner'),
    ('LAB\reviewer', 'Reviewer'),
    ('LAB\readonly', 'ReadOnly')
) AS mapping (Username, RoleName)
INNER JOIN dbo.Users u ON u.Username = mapping.Username
INNER JOIN dbo.Roles r ON r.RoleName = mapping.RoleName
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.UserRoles ur
    WHERE ur.UserId = u.UserId
      AND ur.RoleId = r.RoleId
);
GO
