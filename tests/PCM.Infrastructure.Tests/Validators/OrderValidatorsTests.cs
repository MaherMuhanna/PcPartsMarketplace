using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Validators;

namespace PCM.Infrastructure.Tests.Validators;

public sealed class OrderValidatorsTests
{
    private static CheckoutRequest Checkout(params CheckoutItemRequest[] items) => new() { AddressId = 1, Items = items };

    private static CheckoutItemRequest Item(int id = 1, decimal price = 10m, int quantity = 1) =>
        new() { CartItemId = id, UnitPrice = price, Quantity = quantity };

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Cart_quantity_is_1_to_10(int quantity)
    {
        Assert.False(new AddCartItemRequestValidator().Validate(new AddCartItemRequest { OfferId = 1, Quantity = quantity }).IsValid);
        Assert.False(new UpdateCartItemRequestValidator().Validate(new UpdateCartItemRequest { Quantity = quantity }).IsValid);
    }

    [Fact]
    public void Valid_checkout_passes()
    {
        Assert.True(new CheckoutRequestValidator().Validate(Checkout(Item(1), Item(2, 99.99m, 10))).IsValid);
    }

    [Fact]
    public void Checkout_rejects_empty_duplicate_and_bad_lines()
    {
        var validator = new CheckoutRequestValidator();

        Assert.Contains(validator.Validate(Checkout()).Errors, e => e.PropertyName == "Items");
        Assert.Contains(validator.Validate(Checkout(Item(1), Item(1))).Errors, e => e.PropertyName == "Items");
        Assert.Contains(validator.Validate(Checkout(Item(price: 10.005m))).Errors, e => e.PropertyName == "Items[0].UnitPrice");
        Assert.Contains(validator.Validate(Checkout(Item(quantity: 0))).Errors, e => e.PropertyName == "Items[0].Quantity");
        Assert.Contains(validator.Validate(Checkout(Item()) with { AddressId = 0 }).Errors, e => e.PropertyName == "AddressId");
    }

    [Theory]
    [InlineData("Confirmed", null, true)]
    [InlineData("shipped", null, true)]
    [InlineData("Delivered", null, true)]
    [InlineData("Cancelled", "Out of stock in the warehouse", true)]
    [InlineData("Cancelled", null, false)]        // sellers and admins must give a reason
    [InlineData("Cancelled", "no", false)]
    [InlineData("Pending", null, false)]          // cannot move back to the start
    [InlineData("1", null, false)]                // enum numbers are not accepted
    [InlineData("Refunded", null, false)]
    public void Status_change_rules(string status, string? reason, bool valid)
    {
        var result = new ChangeSellerOrderStatusRequestValidator().Validate(new ChangeSellerOrderStatusRequest { Status = status, Reason = reason });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Customer_reason_is_optional_but_limited()
    {
        var validator = new CancelOrderRequestValidator();

        Assert.True(validator.Validate(new CancelOrderRequest()).IsValid);
        Assert.False(validator.Validate(new CancelOrderRequest { Reason = new string('x', 251) }).IsValid);
    }

    [Theory]
    [InlineData("pending", true)]
    [InlineData("Delivered", true)]
    [InlineData("Lost", false)]
    [InlineData("2", false)]
    public void List_status_filter(string status, bool valid)
    {
        Assert.Equal(valid, new SellerOrderListQueryValidator().Validate(new SellerOrderListQuery { Status = status }).IsValid);
        Assert.Equal(valid, new AdminOrderListQueryValidator().Validate(new AdminOrderListQuery { Status = status }).IsValid);
    }
}
