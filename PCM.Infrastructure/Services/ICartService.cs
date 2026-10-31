using PCM.Infrastructure.DTO.Carts;

namespace PCM.Infrastructure.Services;

/// <summary>
/// The signed-in customer's cart (one per customer, created on the first add). Every method takes the
/// user id from the token; another customer's cart line is "not found" (404). Lines reference offers,
/// and every read shows the offers' current prices and stock.
/// </summary>
public interface ICartService
{
    Task<CartDto> GetCartAsync(int userId, CancellationToken cancellationToken = default);

    Task<CartSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an offer, or increases the quantity of its existing line. The offer must be purchasable (400 on offerId),
    /// the line total 1 to 10 (400 on quantity) and not above the available stock (409); at most 50 lines (409).
    /// </summary>
    Task<CartDto> AddItemAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets a line's quantity (1 to 10, not above available stock: 409; unavailable offer: 409).</summary>
    Task<CartDto> UpdateItemAsync(int userId, int itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);

    Task RemoveItemAsync(int userId, int itemId, CancellationToken cancellationToken = default);

    /// <summary>Removes every line (no-op for an empty or missing cart).</summary>
    Task ClearAsync(int userId, CancellationToken cancellationToken = default);
}
