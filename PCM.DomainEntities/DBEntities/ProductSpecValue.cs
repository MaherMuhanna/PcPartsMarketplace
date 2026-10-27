using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class ProductSpecValue
{
    public int ProductId { get; set; }

    public int SpecAttributeId { get; set; }

    public string? TextValue { get; set; }

    public decimal? NumberValue { get; set; }

    public int? SpecOptionId { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual SpecAttribute SpecAttribute { get; set; } = null!;

    public virtual SpecOption? SpecOption { get; set; }
}
