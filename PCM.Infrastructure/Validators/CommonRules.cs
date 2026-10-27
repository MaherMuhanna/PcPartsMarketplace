using FluentValidation;

namespace PCM.Infrastructure.Validators;

/// <summary>Field rules reused by several validators, so each rule is defined once.</summary>
public static class CommonRules
{
    /// <summary>Phone pattern agreed for V1: optional leading +, then 7 to 15 digits.</summary>
    public const string PhonePattern = @"^\+?[0-9]{7,15}$";

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(name => name.Trim().Length is >= 2 and <= 100)
            .WithMessage("Full name must be between 2 and 100 characters.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

    /// <summary>Same policy as the Identity options: 8+ characters with upper case, lower case and a digit.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

    private const string PhoneMessage = "Phone must be 7 to 15 digits, optionally starting with +.";

    /// <summary>Required phone (non-nullable property).</summary>
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches(PhonePattern)
            .WithMessage(PhoneMessage);

    /// <summary>Optional phone (nullable property); combine with When(...) to skip empty values.</summary>
    public static IRuleBuilderOptions<T, string?> ValidOptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(PhonePattern)
            .WithMessage(PhoneMessage);
}
