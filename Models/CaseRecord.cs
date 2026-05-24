namespace DigitalEvidenceManagementSystem.Models;

public sealed class CaseRecord
{
    public int CaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Open";
    public string Priority { get; set; } = "Normal";
    public int? AssignedExaminerId { get; set; }
    public string? AssignedExaminerName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int EvidenceCount { get; set; }
}
