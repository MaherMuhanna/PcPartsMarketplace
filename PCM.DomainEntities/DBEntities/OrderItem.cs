using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class OrderItem
{
    public int Id { get; set; }

    public int SellerOrderId { get; set; }

    public int OfferId { get; set; }

    public string ProductName { get; set; } = null!;

    public string Condition { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal? LineTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Offer Offer { get; set; } = null!;

    public virtual SellerOrder SellerOrder { get; set; } = null!;
}
