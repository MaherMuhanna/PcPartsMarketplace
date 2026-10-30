using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Validators;

namespace PCM.Infrastructure.Tests.Validators;

public sealed class OfferValidatorsTests
{
    private static CreateOfferRequest ValidCreate() => new() { ProductId = 1, Condition = "New", Price = 99.99m, Stock = 10 };

    [Fact]
    public void Valid_create_passes_with_default_threshold()
    {
        Assert.True(new CreateOfferRequestValidator().Validate(ValidCreate()).IsValid);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("USED")]
    public void Condition_is_case_insensitive(string condition)
    {
        Assert.True(new CreateOfferRequestValidator().Validate(ValidCreate() with { Condition = condition }).IsValid);
    }

    [Theory]
    [InlineData("Refurbished")]
    [InlineData("1")]          // Enum.TryParse would accept the number; only names are valid
    [InlineData("")]
    public void Unknown_condition_fails(string condition)
    {
        var result = new CreateOfferRequestValidator().Validate(ValidCreate() with { Condition = condition });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOfferRequest.Condition));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("12.345")]       // more than 2 decimals
    [InlineData("1000000.00")]   // above 999,999.99
    public void Bad_price_fails(string price)
    {
        var result = new CreateOfferRequestValidator().Validate(ValidCreate() with { Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOfferRequest.Price));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(100_001, null)]
    [InlineData(10, -1)]
    [InlineData(10, 1_001)]
    public void Stock_and_threshold_limits(int stock, int? threshold)
    {
        var result = new CreateOfferRequestValidator().Validate(ValidCreate() with { Stock = stock, LowStockThreshold = threshold });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("Restock", 5, null, true)]
    [InlineData("restock", 5, "Supplier", true)]
    [InlineData("Restock", -5, null, false)]         // restock only adds
    [InlineData("Adjustment", -5, "Stock count", true)]
    [InlineData("Adjustment", 2, "Found 2 units", true)]
    [InlineData("Adjustment", -5, null, false)]      // adjustment needs a note
    [InlineData("Adjustment", -5, "ok", false)]      // note too short
    [InlineData("Adjustment", 0, "Nothing", false)]  // zero is never a change
    [InlineData("Sale", -1, "x", false)]             // order reasons are not for sellers
    [InlineData("0", 5, null, false)]                // numeric enum value
    [InlineData("Restock", 100_001, null, false)]
    public void Stock_change_rules(string type, int change, string? note, bool valid)
    {
        var result = new StockChangeRequestValidator().Validate(new StockChangeRequest { Type = type, Change = change, Note = note });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("low", "available", true)]
    [InlineData("LOW", "-price", true)]
    [InlineData("empty", null, false)]
    [InlineData(null, "oldest", false)]
    public void List_query_stock_and_sort(string? stock, string? sort, bool valid)
    {
        var result = new SellerOfferListQueryValidator().Validate(new SellerOfferListQuery { Stock = stock, Sort = sort });

        Assert.Equal(valid, result.IsValid);
    }
}
