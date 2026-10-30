using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Offers;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Mappings;

/// <summary>Manual mapping for offers and stock movements.</summary>
public static class OfferMappings
{
    public static OfferDto ToDto(this SellerOfferRow row) =>
        new(
            row.Id,
            row.ProductId,
            row.ProductName,
            row.ProductSlug,
            row.ProductModelNumber,
            row.ImageUrl,
            row.ProductIsPublished,
            row.Condition,
            row.Price,
            row.Stock,
            row.ReservedStock,
            OfferStock.Available(row.Stock, row.ReservedStock),
            row.LowStockThreshold,
            OfferStock.IsLow(row.Stock, row.ReservedStock, row.LowStockThreshold),
            row.CreatedAt,
            row.UpdatedAt);

    public static StockMovementDto ToDto(this StockMovement movement) =>
        new(
            movement.Id,
            movement.Reason,
            movement.Change,
            movement.StockAfter,
            movement.ReservedStockAfter,
            movement.Note,
            movement.SellerOrderId,
            movement.CreatedAt);
}
