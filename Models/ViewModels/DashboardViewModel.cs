namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class DashboardViewModel
{
    public int OpenCases { get; set; }
    public int HighPriorityCases { get; set; }
    public int EvidenceItems { get; set; }
    public int HashMismatches { get; set; }
    public IReadOnlyList<CaseRecord> RecentCases { get; set; } = Array.Empty<CaseRecord>();
}
