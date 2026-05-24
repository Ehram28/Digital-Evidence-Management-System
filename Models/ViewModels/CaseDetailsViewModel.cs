namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class CaseDetailsViewModel
{
    public CaseRecord Case { get; set; } = new();
    public IReadOnlyList<EvidenceItem> EvidenceItems { get; set; } = Array.Empty<EvidenceItem>();
}
