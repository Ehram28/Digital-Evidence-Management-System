namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class EvidenceDetailsViewModel
{
    public EvidenceItem Evidence { get; set; } = new();
    public IReadOnlyList<ChainOfCustodyEntry> CustodyEntries { get; set; } = Array.Empty<ChainOfCustodyEntry>();
    public string? StatusMessage { get; set; }
}
