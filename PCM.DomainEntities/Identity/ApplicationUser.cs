using Microsoft.AspNetCore.Identity;
using PCM.DomainEntities.Common;

namespace PCM.DomainEntities.Identity;

/// <summary>
/// Identity user (table dbo.AspNetUsers, int key). Admins, sellers and customers
/// are all users; the role lives in AspNetUserRoles, and sellers also own a
/// SellerProfile row. Users are never deleted: access is controlled through
/// lockout and seller status instead.
/// </summary>
public class ApplicationUser : IdentityUser<int>, IAuditable
{
    /// <summary>Display name, 2 to 100 characters.</summary>
    public string FullName { get; set; } = null!;

    /// <summary>UTC creation time, set by the audit interceptor.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC time of the last update, set by the audit interceptor.</summary>
    public DateTime? UpdatedAt { get; set; }
}
