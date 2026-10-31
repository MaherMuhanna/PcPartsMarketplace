using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Infrastructure.Offers;
using PCM.Infrastructure.Validators;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

public sealed class CartService(IUnitOfWork unitOfWork, ILogger<CartService> logger) : ICartService
{
    public async Task<CartDto> GetCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        var lines = await unitOfWork.Carts.GetLinesAsync(userId, cancellationToken);
        return lines.ToCartDto();
    }

    public async Task<CartSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken = default) =>
        new(await unitOfWork.Carts.CountUnitsAsync(userId, cancellationToken));

    public async Task<CartDto> AddItemAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var offer = await unitOfWork.Offers.GetPurchaseRowAsync(request.OfferId, cancellationToken);
        if (offer is null || !offer.IsPurchasable)
        {
            throw new RequestValidationException("offerId", "This offer is not available.");
        }

        var cart = await unitOfWork.Carts.GetByCustomerAsync(userId, cancellationToken);
        if (cart is null)
        {
            // First add: the cart row is created now (UX_Cart_CustomerId guards a parallel first add: 409).
            cart = new Cart { CustomerId = userId };
            await unitOfWork.Carts.AddAsync(cart, cancellationToken);
        }

        var existing = cart.Id == 0 ? null : await unitOfWork.Carts.GetItemByOfferAsync(cart.Id, offer.Id, cancellationToken);
        var quantity = (existing?.Quantity ?? 0) + request.Quantity;

        if (quantity > CartRules.MaxQuantity)
        {
            throw new RequestValidationException("quantity", existing is null
                ? $"You can buy at most {CartRules.MaxQuantity} units of one offer."
                : $"You can buy at most {CartRules.MaxQuantity} units of one offer; {existing.Quantity} are already in your cart.");
        }

        EnsureAvailable(offer.Stock, offer.ReservedStock, quantity);

        if (existing is not null)
        {
            existing.Quantity = quantity;
        }
        else
        {
            if (cart.Id != 0 && await unitOfWork.Carts.CountLinesAsync(cart.Id, cancellationToken) >= CartRules.MaxLines)
            {
                throw new ConflictException($"Your cart is full ({CartRules.MaxLines} different items). Check out or remove something first.");
            }

            unitOfWork.Carts.AddItem(cart, new CartItem { OfferId = offer.Id, Quantity = quantity });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Customer {UserId} added offer {OfferId} to the cart (quantity now {Quantity})", userId, offer.Id, quantity);

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task<CartDto> UpdateItemAsync(int userId, int itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = await GetOwnedItemAsync(userId, itemId, cancellationToken);
        var offer = await unitOfWork.Offers.GetPurchaseRowAsync(item.OfferId, cancellationToken);
        if (offer is null || !offer.IsPurchasable)
        {
            throw new ConflictException("This offer is no longer available. Remove it from your cart.");
        }

        EnsureAvailable(offer.Stock, offer.ReservedStock, request.Quantity);

        item.Quantity = request.Quantity;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task RemoveItemAsync(int userId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await GetOwnedItemAsync(userId, itemId, cancellationToken);
        unitOfWork.Carts.RemoveItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(int userId, CancellationToken cancellationToken = default)
    {
        var items = await unitOfWork.Carts.GetItemsAsync(userId, cancellationToken);
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            unitOfWork.Carts.RemoveItem(item);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    private async Task<CartItem> GetOwnedItemAsync(int userId, int itemId, CancellationToken cancellationToken) =>
        await unitOfWork.Carts.GetOwnedItemAsync(userId, itemId, cancellationToken)
            ?? throw new NotFoundException($"Cart item {itemId} was not found.");

    /// <summary>Business rule 10: a line may not ask for more than is available now (checkout checks again).</summary>
    private static void EnsureAvailable(int stock, int reservedStock, int quantity)
    {
        var available = Math.Max(0, OfferStock.Available(stock, reservedStock));
        if (quantity > available)
        {
            throw new ConflictException(available == 0
                ? "This offer is out of stock."
                : $"Only {available} unit(s) are available.");
        }
    }
}
