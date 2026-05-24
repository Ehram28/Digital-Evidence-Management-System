namespace DigitalEvidenceManagementSystem.Models;

public sealed class ChainOfCustodyEntry
{
    public long CocId { get; set; }
    public long EvidenceId { get; set; }
    public int CaseId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int PerformedByUserId { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; }
    public string Location { get; set; } = string.Empty;
    public string NarrativeNote { get; set; } = string.Empty;
    public int? FromCustodianId { get; set; }
    public int? ToCustodianId { get; set; }
    public string? FileHashAtAction { get; set; }
    public string? SourceIpAddress { get; set; }
}
