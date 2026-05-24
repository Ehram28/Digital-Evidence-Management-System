namespace DigitalEvidenceManagementSystem.Models;

public sealed class EvidenceItem
{
    public long EvidenceId { get; set; }
    public int CaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string EvidenceType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public bool IsOriginalMedia { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public string VerificationStatus { get; set; } = "Pending";
    public string? Notes { get; set; }
}
