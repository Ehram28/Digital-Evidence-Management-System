using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class EvidenceUploadViewModel
{
    [Required]
    public int CaseId { get; set; }

    public string? CaseNumber { get; set; }

    [Required]
    [Display(Name = "Evidence file")]
    public IFormFile? File { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Evidence type")]
    public string EvidenceType { get; set; } = "Disk image";

    [Display(Name = "Original media")]
    public bool IsOriginalMedia { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = "Digital Forensics Lab";

    [Required, StringLength(2000)]
    [Display(Name = "Chain-of-custody note")]
    public string NarrativeNote { get; set; } = "Evidence uploaded and SHA-256 hash calculated.";

    [StringLength(2000)]
    public string? Notes { get; set; }

    public static readonly IReadOnlyList<string> EvidenceTypes =
    [
        "Disk image",
        "Memory dump",
        "Log file",
        "Mobile extraction",
        "Cloud export",
        "Other"
    ];
}
