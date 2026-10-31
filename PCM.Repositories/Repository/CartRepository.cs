using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories.IRepository;
using PCM.Repositories.Models;

namespace PCM.Repositories.Repository;

public class CartRepository(PcPartsContext context) : Repository<Cart>(context), ICartRepository
{
    // Compile-time constants, so EF inlines them as SQL literals.
    private const string ApprovedSeller = nameof(SellerStatus.Approved);
    private const string ApprovedProduct = nameof(ProductStatus.Approved);

    public Task<Cart?> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

    public async Task<IReadOnlyList<CartLineRow>> GetLinesAsync(int customerId, CancellationToken cancellationToken = default) =>
        await Context.CartItems.AsNoTracking()
            // A line keeps its row when the offer (or its seller / product) is soft-deleted:
            // the customer must see it as "unavailable" instead of it silently disappearing.
            .IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter])
            .Where(i => i.Cart.CustomerId == customerId)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Select(i => new CartLineRow(
                i.Id,
                i.OfferId,
                i.Offer.ProductId,
                i.Offer.Product.Name,
                i.Offer.Product.Slug,
                i.Offer.Product.ProductImages.OrderBy(image => image.SortOrder).ThenBy(image => image.Id).Select(image => image.Url).FirstOrDefault(),
                i.Offer.SellerId,
                i.Offer.Seller.StoreName,
                i.Offer.Condition,
                i.Offer.Price,
                i.Quantity,
                i.Offer.Stock,
                i.Offer.ReservedStock,
                !i.Offer.IsDeleted
                    && !i.Offer.Seller.IsDeleted && i.Offer.Seller.Status == ApprovedSeller
                    && !i.Offer.Product.IsDeleted && i.Offer.Product.IsActive && i.Offer.Product.Status == ApprovedProduct,
                i.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<int> CountUnitsAsync(int customerId, CancellationToken cancellationToken = default) =>
        await Context.CartItems
            .Where(i => i.Cart.CustomerId == customerId)
            .SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0;

    public Task<int> CountLinesAsync(int cartId, CancellationToken cancellationToken = default) =>
        Context.CartItems.CountAsync(i => i.CartId == cartId, cancellationToken);

    public Task<CartItem?> GetOwnedItemAsync(int customerId, int itemId, CancellationToken cancellationToken = default) =>
        Context.CartItems.FirstOrDefaultAsync(i => i.Id == itemId && i.Cart.CustomerId == customerId, cancellationToken);

    public Task<CartItem?> GetItemByOfferAsync(int cartId, int offerId, CancellationToken cancellationToken = default) =>
        Context.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.OfferId == offerId, cancellationToken);

    public Task<List<CartItem>> GetItemsAsync(int customerId, CancellationToken cancellationToken = default) =>
        Context.CartItems
            .Where(i => i.Cart.CustomerId == customerId)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> LockAsync(int customerId, DateTime now, CancellationToken cancellationToken = default)
    {
        // A direct UPDATE on purpose: it takes the row lock now, inside the caller's transaction.
        // A second checkout of the same cart waits here until the first one commits or rolls back.
        var affected = await DbSet
            .Where(c => c.CustomerId == customerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UpdatedAt, (DateTime?)now), cancellationToken);

        return affected == 1;
    }

    public void AddItem(Cart cart, CartItem item)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(item);
        cart.CartItems.Add(item);
    }

    public void RemoveItem(CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Context.CartItems.Remove(item);
    }
}
