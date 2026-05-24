using System.ComponentModel.DataAnnotations;

namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class CaseEditViewModel
{
    public int CaseId { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Case ID")]
    public string CaseNumber { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public string Priority { get; set; } = "Normal";

    public static readonly IReadOnlyList<string> Priorities = ["Low", "Normal", "High", "Critical"];
}
