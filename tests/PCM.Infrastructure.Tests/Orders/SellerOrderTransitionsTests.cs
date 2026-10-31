using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Orders;

namespace PCM.Infrastructure.Tests.Orders;

public sealed class SellerOrderTransitionsTests
{
    [Theory]
    [InlineData(SellerOrderStatus.Pending, SellerOrderStatus.Confirmed)]
    [InlineData(SellerOrderStatus.Confirmed, SellerOrderStatus.Shipped)]
    [InlineData(SellerOrderStatus.Shipped, SellerOrderStatus.Delivered)]
    [InlineData(SellerOrderStatus.Pending, SellerOrderStatus.Cancelled)]
    [InlineData(SellerOrderStatus.Confirmed, SellerOrderStatus.Cancelled)]
    public void Seller_and_admin_can_make_every_flow_move(SellerOrderStatus from, SellerOrderStatus to)
    {
        Assert.True(SellerOrderTransitions.IsAllowed(from, to, OrderActor.Seller));
        Assert.True(SellerOrderTransitions.IsAllowed(from, to, OrderActor.Admin));
    }

    [Theory]
    [InlineData(SellerOrderStatus.Pending, SellerOrderStatus.Shipped)]       // no skipping
    [InlineData(SellerOrderStatus.Pending, SellerOrderStatus.Delivered)]
    [InlineData(SellerOrderStatus.Shipped, SellerOrderStatus.Cancelled)]     // too late to cancel
    [InlineData(SellerOrderStatus.Delivered, SellerOrderStatus.Cancelled)]
    [InlineData(SellerOrderStatus.Cancelled, SellerOrderStatus.Confirmed)]   // final
    [InlineData(SellerOrderStatus.Confirmed, SellerOrderStatus.Pending)]     // no going back
    public void Invalid_moves_are_conflicts(SellerOrderStatus from, SellerOrderStatus to)
    {
        Assert.False(SellerOrderTransitions.IsAllowed(from, to, OrderActor.Admin));
        Assert.Throws<ConflictException>(() => SellerOrderTransitions.EnsureAllowed(from, to, OrderActor.Seller));
    }

    [Fact]
    public void Customers_can_only_cancel_before_shipping()
    {
        Assert.Equal(new[] { SellerOrderStatus.Cancelled }, SellerOrderTransitions.AllowedNext(SellerOrderStatus.Pending, OrderActor.Customer));
        Assert.Equal(new[] { SellerOrderStatus.Cancelled }, SellerOrderTransitions.AllowedNext(SellerOrderStatus.Confirmed, OrderActor.Customer));
        Assert.Empty(SellerOrderTransitions.AllowedNext(SellerOrderStatus.Shipped, OrderActor.Customer));
        Assert.Throws<ConflictException>(() =>
            SellerOrderTransitions.EnsureAllowed(SellerOrderStatus.Pending, SellerOrderStatus.Confirmed, OrderActor.Customer));
    }

    [Fact]
    public void Seller_next_steps_drive_the_buttons()
    {
        Assert.Equal(new[] { SellerOrderStatus.Confirmed, SellerOrderStatus.Cancelled }, SellerOrderTransitions.AllowedNext(SellerOrderStatus.Pending, OrderActor.Seller));
        Assert.Equal(new[] { SellerOrderStatus.Delivered }, SellerOrderTransitions.AllowedNext(SellerOrderStatus.Shipped, OrderActor.Seller));
        Assert.Empty(SellerOrderTransitions.AllowedNext(SellerOrderStatus.Delivered, OrderActor.Seller));
    }

    [Fact]
    public void System_can_only_cancel_a_pending_sub_order()
    {
        Assert.Equal(new[] { SellerOrderStatus.Cancelled }, SellerOrderTransitions.AllowedNext(SellerOrderStatus.Pending, OrderActor.System));
        Assert.Empty(SellerOrderTransitions.AllowedNext(SellerOrderStatus.Confirmed, OrderActor.System));
        Assert.Throws<ConflictException>(() =>
            SellerOrderTransitions.EnsureAllowed(SellerOrderStatus.Confirmed, SellerOrderStatus.Cancelled, OrderActor.System));
        Assert.Throws<ConflictException>(() =>
            SellerOrderTransitions.EnsureAllowed(SellerOrderStatus.Pending, SellerOrderStatus.Confirmed, OrderActor.System));
    }
}

public sealed class SellerOrderWorkflowTests
{
    private const int UserId = 8;
    private static readonly DateTime Now = new(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A sub-order with two lines on two offers; 3 + 1 units reserved.</summary>
    private static (SellerOrder SubOrder, Dictionary<int, Offer> Offers) Arrange(SellerOrderStatus status)
    {
        var offers = new Dictionary<int, Offer>
        {
            [1] = new() { Id = 1, Stock = 10, ReservedStock = 3 },
            [2] = new() { Id = 2, Stock = 4, ReservedStock = 1 }
        };
        var subOrder = new SellerOrder
        {
            Id = 5,
            Status = status.ToString(),
            OrderItems = [new OrderItem { Id = 1, OfferId = 1, Quantity = 3 }, new OrderItem { Id = 2, OfferId = 2, Quantity = 1 }]
        };
        return (subOrder, offers);
    }

    [Fact]
    public void Delivered_sells_every_line()
    {
        var (subOrder, offers) = Arrange(SellerOrderStatus.Shipped);

        SellerOrderWorkflow.Apply(subOrder, offers, SellerOrderStatus.Delivered, OrderActor.Seller, null, UserId, Now);

        Assert.Equal(("Delivered", (DateTime?)Now), (subOrder.Status, subOrder.DeliveredAt));
        Assert.Equal((7, 0), (offers[1].Stock, offers[1].ReservedStock));
        Assert.Equal((3, 0), (offers[2].Stock, offers[2].ReservedStock));
        Assert.All(offers.Values, o => Assert.Equal("Sale", Assert.Single(o.StockMovements).Reason));
    }

    [Fact]
    public void Cancelled_releases_every_line_and_records_who_and_why()
    {
        var (subOrder, offers) = Arrange(SellerOrderStatus.Confirmed);

        SellerOrderWorkflow.Apply(subOrder, offers, SellerOrderStatus.Cancelled, OrderActor.Customer, "  Ordered by mistake ", UserId, Now);

        Assert.Equal(("Cancelled", (DateTime?)Now, "Customer: Ordered by mistake"), (subOrder.Status, subOrder.CancelledAt, subOrder.CancelReason));
        Assert.Equal((10, 0), (offers[1].Stock, offers[1].ReservedStock));
        Assert.Equal((4, 0), (offers[2].Stock, offers[2].ReservedStock));
        Assert.All(offers.Values, o => Assert.Equal("Release", Assert.Single(o.StockMovements).Reason));
    }

    [Fact]
    public void System_cancel_releases_stock_without_a_user()
    {
        var (subOrder, offers) = Arrange(SellerOrderStatus.Pending);

        SellerOrderWorkflow.Apply(subOrder, offers, SellerOrderStatus.Cancelled, OrderActor.System,
            PendingOrderExpiry.ReasonFor(3), userId: null, Now);

        Assert.Equal("System: Not confirmed within 3 days", subOrder.CancelReason);
        Assert.Equal((10, 0), (offers[1].Stock, offers[1].ReservedStock));
        Assert.All(offers.Values, o =>
        {
            var movement = Assert.Single(o.StockMovements);
            Assert.Equal(("Release", (int?)null, "Cancelled by system"), (movement.Reason, movement.CreatedByUserId, movement.Note));
        });
    }

    [Fact]
    public void Confirm_and_ship_only_stamp_the_time()
    {
        var (subOrder, offers) = Arrange(SellerOrderStatus.Pending);

        SellerOrderWorkflow.Apply(subOrder, offers, SellerOrderStatus.Confirmed, OrderActor.Seller, null, UserId, Now);
        SellerOrderWorkflow.Apply(subOrder, offers, SellerOrderStatus.Shipped, OrderActor.Seller, null, UserId, Now.AddHours(2));

        Assert.Equal(("Shipped", (DateTime?)Now, (DateTime?)Now.AddHours(2)), (subOrder.Status, subOrder.ConfirmedAt, subOrder.ShippedAt));
        Assert.All(offers.Values, o => Assert.Empty(o.StockMovements));
    }

    [Fact]
    public void Customer_cancel_without_reason_still_records_the_actor()
    {
        Assert.Equal("Customer: No reason given", SellerOrderWorkflow.FormatCancelReason(OrderActor.Customer, " "));
        Assert.Equal("Customer: ".Length + SellerOrderWorkflow.MaxReasonLength,
            SellerOrderWorkflow.FormatCancelReason(OrderActor.Customer, new string('x', 400)).Length);
    }
}
