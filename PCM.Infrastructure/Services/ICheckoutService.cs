using PCM.Infrastructure.DTO.Orders;

namespace PCM.Infrastructure.Services;

/// <summary>Turns the customer's cart into an order (business rules 1, 2, 3, 8, 9).</summary>
public interface ICheckoutService
{
    /// <summary>
    /// In one transaction: locks the cart, re-reads every offer, compares price, quantity, availability
    /// and stock with what the customer saw (409 <see cref="Exceptions.CheckoutConflictException"/> on any difference),
    /// creates one order with one sub-order per seller, reserves the stock (Reserve movements) and empties
    /// the cart. A concurrency conflict or deadlock is retried once, then 409.
    /// </summary>
    Task<OrderDto> CheckoutAsync(int userId, CheckoutRequest request, CancellationToken cancellationToken = default);
}
