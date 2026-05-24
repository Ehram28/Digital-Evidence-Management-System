namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class CaseListViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public IReadOnlyList<CaseRecord> Cases { get; set; } = Array.Empty<CaseRecord>();

    public static readonly IReadOnlyList<string> Statuses = ["Open", "In Review", "On Hold", "Completed", "Cancelled", "Closed"];
    public static readonly IReadOnlyList<string> Priorities = ["Low", "Normal", "High", "Critical"];
}
