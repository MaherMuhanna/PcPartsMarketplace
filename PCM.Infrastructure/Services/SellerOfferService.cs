using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Infrastructure.Offers;
using PCM.Infrastructure.Validators;
using PCM.Repositories;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Services;

public sealed class SellerOfferService(
    IUnitOfWork unitOfWork,
    ILogger<SellerOfferService> logger) : ISellerOfferService
{
    /// <summary>First try plus one retry after a concurrency conflict; the second conflict becomes a 409.</summary>
    public const int MaxSaveAttempts = 2;

    public async Task<PagedResponse<OfferDto>> GetOffersAsync(int userId, SellerOfferListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);

        var filter = new SellerOfferFilter
        {
            Keyword = query.Q,
            Condition = NormalizeCondition(query.Condition),
            Stock = ParseStockFilter(query.Stock)
        };

        var page = await unitOfWork.Offers.GetSellerPagedAsync(
            sellerId, filter, query.Page, query.PageSize, ParseSort(query.Sort), cancellationToken);
        return page.ToResponse(row => row.ToDto());
    }

    public Task<PagedResponse<OfferDto>> GetLowStockAsync(int userId, SellerOfferListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return GetOffersAsync(userId, new SellerOfferListQuery
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Sort = query.Sort ?? "available",
            Q = query.Q,
            Condition = query.Condition,
            Stock = "low"
        }, cancellationToken);
    }

    public async Task<OfferDto> GetOfferAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        return await GetRowAsync(sellerId, id, cancellationToken);
    }

    public async Task<OfferDto> CreateAsync(int userId, CreateOfferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        var condition = NormalizeCondition(request.Condition)
            ?? throw new RequestValidationException("condition", OfferRules.ConditionMessage);

        if (!await unitOfWork.Products.IsPublishedAsync(request.ProductId, cancellationToken))
        {
            throw new RequestValidationException("productId", "Offers can only be added to published catalog products.");
        }

        // The filtered unique index UX_Offer_Product_Seller_Condition is the final guard (race -> 409).
        if (await unitOfWork.Offers.ExistsForSellerAsync(sellerId, request.ProductId, condition, cancellationToken))
        {
            throw new ConflictException($"You already have a {condition} offer for this product. Edit that offer instead.");
        }

        var offer = new Offer
        {
            ProductId = request.ProductId,
            SellerId = sellerId,
            Condition = condition,
            Price = request.Price,
            Stock = 0,
            ReservedStock = 0,
            LowStockThreshold = request.LowStockThreshold ?? OfferRules.DefaultLowStockThreshold
        };

        // Initial stock goes through the same path as every later change, so history starts complete.
        if (request.Stock > 0)
        {
            OfferStock.Apply(offer, StockMovementReason.Restock, request.Stock, userId, "Initial stock");
        }

        await unitOfWork.Offers.AddAsync(offer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seller {SellerId} created offer {OfferId} on product {ProductId} ({Condition}, stock {Stock})",
            sellerId, offer.Id, offer.ProductId, offer.Condition, offer.Stock);
        return await GetRowAsync(sellerId, offer.Id, cancellationToken);
    }

    public async Task<OfferDto> UpdateAsync(int userId, int id, UpdateOfferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);

        await SaveWithRetryAsync(async () =>
        {
            var offer = await GetOwnedAsync(sellerId, id, cancellationToken);
            offer.Price = request.Price;
            offer.LowStockThreshold = request.LowStockThreshold;
        }, id, cancellationToken);

        return await GetRowAsync(sellerId, id, cancellationToken);
    }

    public async Task<OfferDto> ChangeStockAsync(int userId, int id, StockChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);

        // The validator already checked the type; this keeps the service safe on its own.
        if (!OfferRules.TryParseChangeType(request.Type, out var reason))
        {
            throw new RequestValidationException("type", "Type must be Restock or Adjustment.");
        }

        StockMovement? movement = null;
        await SaveWithRetryAsync(async () =>
        {
            var offer = await GetOwnedAsync(sellerId, id, cancellationToken);
            movement = OfferStock.Apply(offer, reason, request.Change, userId, request.Note);
        }, id, cancellationToken);

        logger.LogInformation("Seller {SellerId} changed stock of offer {OfferId}: {Reason} {Change:+#;-#} -> {StockAfter}",
            sellerId, id, reason, request.Change, movement!.StockAfter);
        return await GetRowAsync(sellerId, id, cancellationToken);
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);

        await SaveWithRetryAsync(async () =>
        {
            var offer = await GetOwnedAsync(sellerId, id, cancellationToken);
            if (offer.ReservedStock > 0)
            {
                throw new ConflictException(
                    $"{offer.ReservedStock} unit(s) of this offer are reserved by open orders. Delete it after those orders are completed or cancelled.");
            }

            unitOfWork.Offers.Remove(offer);    // soft delete (audit interceptor)
        }, id, cancellationToken);

        logger.LogInformation("Seller {SellerId} deleted offer {OfferId}", sellerId, id);
    }

    public async Task<PagedResponse<StockMovementDto>> GetMovementsAsync(
        int userId, int id, StockMovementListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);

        // Ownership first: another seller's offer is 404, and its history is never read.
        if (!await unitOfWork.Offers.ExistsAsync(o => o.Id == id && o.SellerId == sellerId, cancellationToken))
        {
            throw new NotFoundException($"Offer {id} was not found.");
        }

        var page = await unitOfWork.StockMovements.GetPagedByOfferAsync(id, query.Page, query.PageSize, cancellationToken);
        return page.ToResponse(m => m.ToDto());
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Runs <paramref name="stage"/> (load the offer and change it) and saves. If another request
    /// changed the same offer in between (RowVersion mismatch), the tracked state is discarded and
    /// the whole step runs again on fresh data, so the business rules are checked against the new
    /// numbers. A second conflict is left to the API, which answers 409.
    /// </summary>
    private async Task SaveWithRetryAsync(Func<Task> stage, int offerId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await stage();

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                logger.LogWarning("Concurrency conflict on offer {OfferId} (attempt {Attempt}); retrying with fresh data", offerId, attempt);
                unitOfWork.ClearChanges();
            }
        }
    }

    private async Task<int> GetSellerIdAsync(int userId, CancellationToken cancellationToken)
    {
        // The ApprovedSeller policy has already checked the profile; this only maps user -> seller id.
        var profile = await unitOfWork.SellerProfiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Seller profile was not found.");
        return profile.Id;
    }

    /// <summary>The seller's own offer (tracked), or 404 (also when it belongs to someone else).</summary>
    private async Task<Offer> GetOwnedAsync(int sellerId, int id, CancellationToken cancellationToken) =>
        await unitOfWork.Offers.GetOwnedAsync(sellerId, id, cancellationToken)
            ?? throw new NotFoundException($"Offer {id} was not found.");

    private async Task<OfferDto> GetRowAsync(int sellerId, int id, CancellationToken cancellationToken)
    {
        var row = await unitOfWork.Offers.GetSellerRowAsync(sellerId, id, cancellationToken)
            ?? throw new NotFoundException($"Offer {id} was not found.");
        return row.ToDto();
    }

    /// <summary>"new" / "USED" -> "New" / "Used" (the CHECK constraint values); null when empty or invalid.</summary>
    public static string? NormalizeCondition(string? value) =>
        OfferRules.IsCondition(value) ? Enum.Parse<OfferCondition>(value!, ignoreCase: true).ToString() : null;

    public static OfferStockFilter ParseStockFilter(string? value) => value?.ToLowerInvariant() switch
    {
        "in" => OfferStockFilter.InStock,
        "low" => OfferStockFilter.Low,
        "out" => OfferStockFilter.OutOfStock,
        _ => OfferStockFilter.All
    };

    public static SellerOfferSort ParseSort(string? value) => value?.ToLowerInvariant() switch
    {
        "name" => SellerOfferSort.Name,
        "price" => SellerOfferSort.PriceAsc,
        "-price" => SellerOfferSort.PriceDesc,
        "available" => SellerOfferSort.Available,
        _ => SellerOfferSort.Newest
    };
}
