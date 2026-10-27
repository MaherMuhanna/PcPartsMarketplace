using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class Order
{
    public int Id { get; set; }

    public string? OrderNumber { get; set; }

    public int CustomerId { get; set; }

    public int ShippingAddressId { get; set; }

    public decimal Total { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<SellerOrder> SellerOrders { get; set; } = new List<SellerOrder>();

    public virtual Address ShippingAddress { get; set; } = null!;
}
