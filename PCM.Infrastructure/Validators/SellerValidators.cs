using FluentValidation;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.Infrastructure.Validators;

public sealed class SellerListQueryValidator : PageQueryValidator<SellerListQuery>
{
    public SellerListQueryValidator() : base(SellerListQuery.AllowedSorts)
    {
        RuleFor(q => q.Status)
            .Must(status => status is null || Enum.TryParse<SellerStatus>(status, ignoreCase: true, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<SellerStatus>())}.");
    }
}

public sealed class SellerStatusReasonRequestValidator : AbstractValidator<SellerStatusReasonRequest>
{
    public SellerStatusReasonRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .Must(reason => reason.Trim().Length is >= 3 and <= 500)
            .WithMessage("Reason must be between 3 and 500 characters.");
    }
}

public sealed class UpdateSellerProfileRequestValidator : AbstractValidator<UpdateSellerProfileRequest>
{
    public UpdateSellerProfileRequestValidator()
    {
        RuleFor(r => r.Description).MaximumLength(500);

        RuleFor(r => r.ContactPhone)
            .ValidOptionalPhone()
            .When(r => !string.IsNullOrWhiteSpace(r.ContactPhone));
    }
}
