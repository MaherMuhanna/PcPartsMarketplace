using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Offers;

namespace PCM.Infrastructure.Tests.Offers;

public sealed class OfferStockTests
{
    private const int UserId = 9;

    private static Offer NewOffer(int stock, int reserved) => new() { Id = 1, Stock = stock, ReservedStock = reserved };

    [Fact]
    public void Restock_adds_stock_and_records_the_movement()
    {
        var offer = NewOffer(stock: 5, reserved: 2);

        var movement = OfferStock.Apply(offer, StockMovementReason.Restock, 10, UserId, "  Supplier delivery ");

        Assert.Equal(15, offer.Stock);
        Assert.Same(movement, Assert.Single(offer.StockMovements));
        Assert.Equal("Restock", movement.Reason);
        Assert.Equal(10, movement.Change);
        Assert.Equal(15, movement.StockAfter);
        Assert.Equal(2, movement.ReservedStockAfter);
        Assert.Equal(UserId, movement.CreatedByUserId);
        Assert.Equal("Supplier delivery", movement.Note);   // trimmed
    }

    [Fact]
    public void Adjustment_can_remove_down_to_the_reserved_quantity()
    {
        var offer = NewOffer(stock: 10, reserved: 4);

        var movement = OfferStock.Apply(offer, StockMovementReason.Adjustment, -6, UserId, "Stock count");

        Assert.Equal(4, offer.Stock);
        Assert.Equal(-6, movement.Change);
        Assert.Equal(4, movement.StockAfter);
    }

    [Fact]
    public void Stock_below_reserved_is_a_conflict_and_changes_nothing()
    {
        var offer = NewOffer(stock: 10, reserved: 4);

        var error = Assert.Throws<ConflictException>(() => OfferStock.Apply(offer, StockMovementReason.Adjustment, -7, UserId, "Too many"));

        Assert.Contains("reserved", error.Message);
        Assert.Equal(10, offer.Stock);
        Assert.Empty(offer.StockMovements);
    }

    [Fact]
    public void Negative_stock_is_a_conflict()
    {
        var offer = NewOffer(stock: 3, reserved: 0);

        Assert.Throws<ConflictException>(() => OfferStock.Apply(offer, StockMovementReason.Adjustment, -4, UserId, "Lost"));
        Assert.Equal(3, offer.Stock);
    }

    [Fact]
    public void Zero_change_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OfferStock.Apply(NewOffer(1, 0), StockMovementReason.Restock, 0, UserId, null));
    }

    [Fact]
    public void Blank_note_is_stored_as_null()
    {
        var movement = OfferStock.Apply(NewOffer(0, 0), StockMovementReason.Restock, 1, UserId, "   ");

        Assert.Null(movement.Note);
    }

    [Theory]
    [InlineData(10, 0, 5, false)]   // 10 available, alert at 5
    [InlineData(5, 0, 5, true)]     // equal to the threshold counts as low
    [InlineData(9, 5, 5, true)]     // 4 available: reserved units are not available
    [InlineData(0, 0, 0, true)]     // out of stock is always low
    [InlineData(1, 0, 0, false)]    // threshold 0: only out of stock is low
    public void Low_stock_uses_available_quantity(int stock, int reserved, int threshold, bool expected)
    {
        Assert.Equal(expected, OfferStock.IsLow(stock, reserved, threshold));
    }
}
