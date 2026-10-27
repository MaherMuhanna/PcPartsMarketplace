using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class StockMovement
{
    public int Id { get; set; }

    public int OfferId { get; set; }

    public string Reason { get; set; } = null!;

    public int Change { get; set; }

    public int StockAfter { get; set; }

    public int ReservedStockAfter { get; set; }

    public int? SellerOrderId { get; set; }

    public int? CreatedByUserId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Offer Offer { get; set; } = null!;

    public virtual SellerOrder? SellerOrder { get; set; }
}
