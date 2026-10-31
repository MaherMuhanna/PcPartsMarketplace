using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Offers;

namespace PCM.Infrastructure.Tests.Offers;

/// <summary>Reserve / Release / Sale: the stock effects of checkout, cancellation and delivery (business rules 2, 5, 6).</summary>
public sealed class OfferStockOrderTests
{
    private const int UserId = 4;

    private static Offer NewOffer(int stock, int reserved) => new() { Id = 1, Stock = stock, ReservedStock = reserved };

    [Fact]
    public void Reserve_holds_units_without_touching_stock()
    {
        var offer = NewOffer(stock: 10, reserved: 2);
        var subOrder = new SellerOrder();

        var movement = OfferStock.Reserve(offer, subOrder, 3, UserId);

        Assert.Equal((10, 5), (offer.Stock, offer.ReservedStock));
        Assert.Equal(("Reserve", 3, 10, 5), (movement.Reason, movement.Change, movement.StockAfter, movement.ReservedStockAfter));
        Assert.Same(subOrder, movement.SellerOrder);
        Assert.Same(movement, Assert.Single(offer.StockMovements));
    }

    [Fact]
    public void Reserve_more_than_available_is_a_conflict()
    {
        var offer = NewOffer(stock: 5, reserved: 4);

        Assert.Throws<ConflictException>(() => OfferStock.Reserve(offer, new SellerOrder(), 2, UserId));
        Assert.Equal(4, offer.ReservedStock);
        Assert.Empty(offer.StockMovements);
    }

    [Fact]
    public void Release_gives_reserved_units_back()
    {
        var offer = NewOffer(stock: 10, reserved: 5);

        var movement = OfferStock.Release(offer, new SellerOrder(), 3, UserId, "Cancelled by customer");

        Assert.Equal((10, 2), (offer.Stock, offer.ReservedStock));
        Assert.Equal(("Release", -3, "Cancelled by customer"), (movement.Reason, movement.Change, movement.Note));
    }

    [Fact]
    public void Sell_removes_units_from_stock_and_reserved()
    {
        var offer = NewOffer(stock: 10, reserved: 5);

        var movement = OfferStock.Sell(offer, new SellerOrder(), 3, UserId);

        Assert.Equal((7, 2), (offer.Stock, offer.ReservedStock));
        Assert.Equal(OfferStock.Available(10, 5), OfferStock.Available(offer.Stock, offer.ReservedStock));   // available unchanged
        Assert.Equal(("Sale", -3, 7, 2), (movement.Reason, movement.Change, movement.StockAfter, movement.ReservedStockAfter));
    }

    [Theory]
    [InlineData(StockMovementReason.Release)]
    [InlineData(StockMovementReason.Sale)]
    public void Releasing_or_selling_more_than_reserved_is_a_bug(StockMovementReason reason)
    {
        var offer = NewOffer(stock: 10, reserved: 1);

        Assert.Throws<InvalidOperationException>(() => reason == StockMovementReason.Sale
            ? OfferStock.Sell(offer, new SellerOrder(), 2, UserId)
            : OfferStock.Release(offer, new SellerOrder(), 2, UserId, null));
    }

    [Fact]
    public void Purchasable_needs_approved_seller_and_published_product()
    {
        Offer Make(string sellerStatus = "Approved", string productStatus = "Approved", bool active = true, bool deleted = false) => new()
        {
            IsDeleted = deleted,
            Seller = new SellerProfile { Status = sellerStatus },
            Product = new Product { Status = productStatus, IsActive = active }
        };

        Assert.True(OfferStock.IsPurchasable(Make()));
        Assert.False(OfferStock.IsPurchasable(Make(sellerStatus: "Suspended")));
        Assert.False(OfferStock.IsPurchasable(Make(productStatus: "Pending")));
        Assert.False(OfferStock.IsPurchasable(Make(active: false)));
        Assert.False(OfferStock.IsPurchasable(Make(deleted: true)));
    }
}
