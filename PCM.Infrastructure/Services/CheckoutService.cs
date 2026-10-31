using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Infrastructure.Offers;
using PCM.Infrastructure.Orders;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

public sealed class CheckoutService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    /// <summary>First try plus one retry after a concurrency conflict or deadlock; the second one becomes a 409.</summary>
    public const int MaxAttempts = 2;

    /// <summary>SQL Server error number of "chosen as the deadlock victim".</summary>
    private const int DeadlockErrorNumber = 1205;

    public async Task<OrderDto> CheckoutAsync(int userId, CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var orderId = await PlaceOrderAsync(userId, request, cancellationToken);

                var order = await unitOfWork.Orders.GetDetailsAsync(orderId, userId, cancellationToken)
                    ?? throw new InvalidOperationException($"Order {orderId} was not found right after checkout.");
                return order.ToDto(OrderActor.Customer, customer: null);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                // The transaction was rolled back (disposed): nothing was saved. Start again on fresh rows,
                // so price and stock are checked against the numbers that won.
                logger.LogWarning(ex, "Checkout of customer {UserId} hit a {Kind} (attempt {Attempt}); retrying",
                    userId, ex is DbUpdateConcurrencyException ? "concurrency conflict" : "deadlock", attempt);
                unitOfWork.ClearChanges();
            }
        }
    }

    /// <summary>One attempt, all or nothing. Returns the new order id.</summary>
    private async Task<int> PlaceOrderAsync(int userId, CheckoutRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 1. Serialize checkouts of this cart: a double click waits here, then finds the cart empty.
        List<CartItem> items = await unitOfWork.Carts.LockAsync(userId, now, cancellationToken)
            ? await unitOfWork.Carts.GetItemsAsync(userId, cancellationToken)
            : [];
        if (items.Count == 0)
        {
            throw new RequestValidationException("items", "Your cart is empty.");
        }

        // 2. The address must be one of the customer's own (not found and not yours look the same).
        var address = await unitOfWork.Addresses.GetOwnedAsync(request.AddressId, userId, cancellationToken)
            ?? throw new RequestValidationException("addressId", "Choose one of your saved addresses.");

        // 3. Re-read every offer (tracked: the reservations below are checked by Offer.RowVersion on save).
        var offerIds = items.Select(i => i.OfferId).Distinct().ToList();
        var offers = (await unitOfWork.Offers.GetForUpdateAsync(offerIds, cancellationToken)).ToDictionary(o => o.Id);

        var issues = CheckoutRules.FindIssues(items, offers, request.Items);
        if (issues.Count > 0)
        {
            logger.LogInformation("Checkout of customer {UserId} stopped: {Problems}",
                userId, string.Join(", ", issues.Select(i => $"{i.CartItemId}:{i.Problem}")));
            throw new CheckoutConflictException("Some items in your cart changed. Review your cart and try again.", issues);
        }

        // 4. One order, one sub-order per seller, a snapshot line per cart line, stock reserved per line.
        var order = new Order
        {
            CustomerId = userId,
            ShippingAddressId = address.Id,
            PaymentMethod = nameof(PaymentMethod.COD)
        };

        foreach (var sellerItems in items.GroupBy(i => offers[i.OfferId].SellerId).OrderBy(g => g.Key))
        {
            var sellerOrder = new SellerOrder
            {
                SellerId = sellerItems.Key,
                Status = nameof(SellerOrderStatus.Pending)
            };

            foreach (var item in sellerItems)
            {
                var offer = offers[item.OfferId];
                sellerOrder.OrderItems.Add(new OrderItem
                {
                    OfferId = offer.Id,
                    ProductName = offer.Product.Name,
                    Condition = offer.Condition,
                    UnitPrice = offer.Price,
                    Quantity = item.Quantity
                });

                OfferStock.Reserve(offer, sellerOrder, item.Quantity, userId);
            }

            sellerOrder.Subtotal = sellerOrder.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
            order.SellerOrders.Add(sellerOrder);
        }

        // Business rule 9: the total is the sum of the sub-order subtotals.
        order.Total = order.SellerOrders.Sum(s => s.Subtotal);
        await unitOfWork.Orders.AddAsync(order, cancellationToken);

        // 5. Empty the cart in the same transaction.
        foreach (var item in items)
        {
            unitOfWork.Carts.RemoveItem(item);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Customer {UserId} placed order {OrderId} ({SellerCount} seller(s), total {Total})",
            userId, order.Id, order.SellerOrders.Count, order.Total);
        return order.Id;
    }

    /// <summary>Conflicts that a fresh attempt can resolve: RowVersion mismatch, or SQL Server picked us as a deadlock victim.</summary>
    private static bool IsTransient(Exception exception) =>
        exception is DbUpdateConcurrencyException
            or DbUpdateException { InnerException: SqlException { Number: DeadlockErrorNumber } }
            or SqlException { Number: DeadlockErrorNumber };
}
