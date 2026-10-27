using FluentValidation;
using PCM.Infrastructure.DTO.Auth;

namespace PCM.Infrastructure.Validators;

public sealed class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).StrongPassword();
    }
}

public sealed class RegisterSellerRequestValidator : AbstractValidator<RegisterSellerRequest>
{
    public RegisterSellerRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).StrongPassword();

        RuleFor(r => r.StoreName)
            .NotEmpty()
            .Must(name => name.Trim().Length is >= 3 and <= 100)
            .WithMessage("Store name must be between 3 and 100 characters.");

        RuleFor(r => r.Description).MaximumLength(500);

        RuleFor(r => r.ContactPhone)
            .ValidOptionalPhone()
            .When(r => !string.IsNullOrWhiteSpace(r.ContactPhone));
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(100);
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
    }
}
