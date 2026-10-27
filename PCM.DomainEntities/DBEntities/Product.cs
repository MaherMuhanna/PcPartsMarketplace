using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public int BrandId { get; set; }

    public string Name { get; set; } = null!;

    public string ModelNumber { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public int? ProposedBySellerId { get; set; }

    public string? ReviewNote { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Brand Brand { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductSpecValue> ProductSpecValues { get; set; } = new List<ProductSpecValue>();

    public virtual SellerProfile? ProposedBySeller { get; set; }
}
