using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Models;

namespace PCM.Repositories.IRepository;

/// <summary>
/// One cart per customer. Every method takes the customer id from the token, so another customer's
/// cart line is simply "not found". Lines are hard-deleted (owned children of the cart).
/// </summary>
public interface ICartRepository : IRepository<Cart>
{
    /// <summary>Tracked cart of the customer (without lines), or null if they never added anything.</summary>
    Task<Cart?> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Read-only lines of the customer's cart with live offer data, oldest first.</summary>
    Task<IReadOnlyList<CartLineRow>> GetLinesAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Total units in the customer's cart (0 without a cart).</summary>
    Task<int> CountUnitsAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Number of distinct lines in a cart.</summary>
    Task<int> CountLinesAsync(int cartId, CancellationToken cancellationToken = default);

    /// <summary>Tracked line if it exists and is in the customer's cart, otherwise null.</summary>
    Task<CartItem?> GetOwnedItemAsync(int customerId, int itemId, CancellationToken cancellationToken = default);

    /// <summary>Tracked line of the cart for this offer, or null.</summary>
    Task<CartItem?> GetItemByOfferAsync(int cartId, int offerId, CancellationToken cancellationToken = default);

    /// <summary>Tracked lines of the customer's cart (empty without a cart).</summary>
    Task<List<CartItem>> GetItemsAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the customer's cart row until the current transaction ends (an UPDATE takes an exclusive
    /// row lock), so two checkouts of the same cart run one after the other: the second one then finds
    /// the cart empty. Must be called inside <see cref="IUnitOfWork.BeginTransactionAsync"/>.
    /// Runs immediately (not staged). Returns false when the customer has no cart.
    /// </summary>
    Task<bool> LockAsync(int customerId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Stages a new line in the cart.</summary>
    void AddItem(Cart cart, CartItem item);

    /// <summary>Stages a line for deletion (hard delete).</summary>
    void RemoveItem(CartItem item);
}
