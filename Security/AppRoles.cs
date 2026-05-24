namespace DigitalEvidenceManagementSystem.Security;

public sealed record AppRoleOption(string Name, string DisplayName, string Description);

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Examiner = "Examiner";
    public const string Reviewer = "Reviewer";
    public const string ReadOnly = "ReadOnly";

    public const string AnyLabUser = Administrator + "," + Examiner + "," + Reviewer + "," + ReadOnly;
    public const string CaseWriters = Administrator + "," + Examiner;
    public const string EvidenceWriters = Administrator + "," + Examiner;
    public const string EvidenceReviewers = Administrator + "," + Examiner + "," + Reviewer;

    public static readonly IReadOnlyList<AppRoleOption> ForensicTeamRoles =
    [
        new(Administrator, "Administrator - complete access", "Can manage users, cases, evidence, and system settings."),
        new(Examiner, "Examiner - create and upload", "Can create cases, upload evidence, and add chain-of-custody entries."),
        new(Reviewer, "Reviewer - review and verify", "Can review case and evidence records."),
        new(ReadOnly, "ReadOnly - view only", "Can view permitted case records without modifying evidence.")
    ];

    public static bool TryGetForensicTeamRole(string? roleName, out AppRoleOption? role)
    {
        role = ForensicTeamRoles.FirstOrDefault(option => option.Name == roleName);
        return role is not null;
    }
}
