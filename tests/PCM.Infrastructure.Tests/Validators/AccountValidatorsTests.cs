using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Validators;

namespace PCM.Infrastructure.Tests.Validators;

public sealed class AccountValidatorsTests
{
    private static RegisterSellerRequest ValidSeller() => new()
    {
        FullName = "Omar Haddad",
        Email = "omar@example.com",
        Password = "Strong123",
        StoreName = "Tech Hub",
        ContactPhone = "+962790000000"
    };

    [Fact]
    public void Valid_seller_registration_passes()
    {
        Assert.True(new RegisterSellerRequestValidator().Validate(ValidSeller()).IsValid);
    }

    [Theory]
    [InlineData("short1A")]       // 7 characters
    [InlineData("alllower1")]     // no upper case
    [InlineData("ALLUPPER1")]     // no lower case
    [InlineData("NoDigitsHere")]  // no digit
    public void Weak_passwords_fail(string password)
    {
        var result = new RegisterSellerRequestValidator().Validate(ValidSeller() with { Password = password });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterSellerRequest.Password));
    }

    [Theory]
    [InlineData("ab")]                  // too short
    [InlineData("   ")]                 // blank
    public void Store_name_must_be_3_to_100_characters(string storeName)
    {
        var result = new RegisterSellerRequestValidator().Validate(ValidSeller() with { StoreName = storeName });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterSellerRequest.StoreName));
    }

    [Theory]
    [InlineData(null, true)]            // optional for sellers
    [InlineData("", true)]
    [InlineData("0791234567", true)]
    [InlineData("+962791234567", true)]
    [InlineData("12345", false)]        // too short
    [InlineData("07-9123-4567", false)] // separators not allowed
    public void Seller_contact_phone_is_optional_but_must_match_the_pattern(string? phone, bool valid)
    {
        var result = new RegisterSellerRequestValidator().Validate(ValidSeller() with { ContactPhone = phone });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Address_requires_city_street_and_phone()
    {
        var result = new CreateAddressRequestValidator().Validate(new CreateAddressRequest());

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAddressRequest.City));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAddressRequest.Street));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAddressRequest.Phone));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("no", false)]
    [InlineData("Late deliveries", true)]
    public void Status_reason_must_be_3_to_500_characters(string reason, bool valid)
    {
        Assert.Equal(valid, new SellerStatusReasonRequestValidator().Validate(new SellerStatusReasonRequest { Reason = reason }).IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("pending", true)]       // case-insensitive
    [InlineData("Banned", false)]
    public void Seller_list_status_filter_must_be_a_known_status(string? status, bool valid)
    {
        Assert.Equal(valid, new SellerListQueryValidator().Validate(new SellerListQuery { Status = status }).IsValid);
    }
}
