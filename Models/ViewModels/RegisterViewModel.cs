using System.ComponentModel.DataAnnotations;
using DigitalEvidenceManagementSystem.Security;

namespace DigitalEvidenceManagementSystem.Models.ViewModels;

public sealed class RegisterViewModel
{
    [Required]
    [StringLength(128, MinimumLength = 3)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }

    [Required]
    [Display(Name = "Forensic team role")]
    public string Role { get; set; } = AppRoles.ReadOnly;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
