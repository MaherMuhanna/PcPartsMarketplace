using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class SpecOption
{
    public int Id { get; set; }

    public int SpecAttributeId { get; set; }

    public string Value { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<ProductSpecValue> ProductSpecValues { get; set; } = new List<ProductSpecValue>();

    public virtual SpecAttribute SpecAttribute { get; set; } = null!;
}
