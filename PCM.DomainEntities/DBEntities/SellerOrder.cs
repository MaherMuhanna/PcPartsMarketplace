using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class SellerOrder
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int SellerId { get; set; }

    public string Status { get; set; } = null!;

    public decimal Subtotal { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual SellerProfile Seller { get; set; } = null!;

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
