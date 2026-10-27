using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class SellerProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string StoreName { get; set; } = null!;

    public string? Description { get; set; }

    public string? ContactPhone { get; set; }

    public string Status { get; set; } = null!;

    public string? StatusReason { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<SellerOrder> SellerOrders { get; set; } = new List<SellerOrder>();
}
