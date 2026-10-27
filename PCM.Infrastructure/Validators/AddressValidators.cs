using FluentValidation;
using PCM.Infrastructure.DTO.Addresses;

namespace PCM.Infrastructure.Validators;

public sealed class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(r => r.City).NotEmpty().Length(2, 100);
        RuleFor(r => r.Street).NotEmpty().Length(2, 200);
        RuleFor(r => r.Details).MaximumLength(200);
        RuleFor(r => r.Phone).ValidPhone();
    }
}

public sealed class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(r => r.City).NotEmpty().Length(2, 100);
        RuleFor(r => r.Street).NotEmpty().Length(2, 200);
        RuleFor(r => r.Details).MaximumLength(200);
        RuleFor(r => r.Phone).ValidPhone();
    }
}

public sealed class AddressListQueryValidator() : PageQueryValidator<AddressListQuery>(AddressListQuery.AllowedSorts);
