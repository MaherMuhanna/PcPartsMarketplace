using System;
using System.Collections.Generic;

namespace PCM.DomainEntities.DBEntities;

public partial class Address
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string City { get; set; } = null!;

    public string Street { get; set; } = null!;

    public string? Details { get; set; }

    public string Phone { get; set; } = null!;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
