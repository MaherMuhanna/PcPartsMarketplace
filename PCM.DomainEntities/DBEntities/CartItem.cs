using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class CartItem
{
    public int Id { get; set; }

    public int CartId { get; set; }

    public int OfferId { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual Offer Offer { get; set; } = null!;
}
