using System.ComponentModel.DataAnnotations;
using PCM.Infrastructure.Validators;

namespace PCM.UI.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

/// <summary>Account type chosen on the register page tabs.</summary>
public enum AccountType
{
    Customer,
    Seller
}

/// <summary>
/// One form for both account types. Basic checks run here for quick feedback;
/// the API validates everything again (FluentValidation) and its field errors are shown too.
/// </summary>
public sealed class RegisterViewModel : IValidatableObject
{
    public AccountType AccountType { get; set; } = AccountType.Customer;

    [Required, Display(Name = "Full name"), StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // ---- Seller only ----

    [Display(Name = "Store name"), StringLength(100)]
    public string? StoreName { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Contact phone"), RegularExpression(CommonRules.PhonePattern, ErrorMessage = "Phone must be 7 to 15 digits, optionally starting with +.")]
    public string? ContactPhone { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AccountType == AccountType.Seller && string.IsNullOrWhiteSpace(StoreName))
        {
            yield return new ValidationResult("Store name is required for a seller account.", [nameof(StoreName)]);
        }
    }
}
