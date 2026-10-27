using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class SpecAttribute
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string DataType { get; set; } = null!;

    public string? Unit { get; set; }

    public bool IsFilterable { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<ProductSpecValue> ProductSpecValues { get; set; } = new List<ProductSpecValue>();

    public virtual ICollection<SpecOption> SpecOptions { get; set; } = new List<SpecOption>();
}
