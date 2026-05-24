namespace DigitalEvidenceManagementSystem.Security;

public sealed record AppRoleOption(string Name, string DisplayName, string Description);

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Examiner = "Examiner";
    public const string Investigator = Examiner;
    public const string Reviewer = "Reviewer";
    public const string ReadOnly = "ReadOnly";

    public const string AnyLabUser = Administrator + "," + Examiner + "," + Reviewer + "," + ReadOnly;
    public const string CaseWriters = Administrator + "," + Examiner;
    public const string EvidenceWriters = Administrator + "," + Examiner;
    public const string EvidenceVerifiers = Administrator + "," + Examiner + "," + Reviewer;
    public const string EvidenceDownloaders = Administrator + "," + Examiner;

    public static readonly IReadOnlyList<AppRoleOption> ForensicTeamRoles =
    [
        new(Administrator, "Administrator - complete access", "Can manage users, cases, evidence, and system settings."),
        new(Investigator, "Investigator - create and upload", "Can create cases, upload evidence, and add chain-of-custody entries."),
        new(Reviewer, "Reviewer - verify only", "Can review records and verify evidence integrity without downloading evidence."),
        new(ReadOnly, "Reader - view only", "Can view permitted case records without modifying evidence.")
    ];

    public static bool TryGetForensicTeamRole(string? roleName, out AppRoleOption? role)
    {
        role = ForensicTeamRoles.FirstOrDefault(option => option.Name == roleName);
        return role is not null;
    }

    public static string ToDisplayName(string roleName)
    {
        return roleName switch
        {
            Examiner => "Investigator",
            ReadOnly => "Reader",
            _ => roleName
        };
    }
}
