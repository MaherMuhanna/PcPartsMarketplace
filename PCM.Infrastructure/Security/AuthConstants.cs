namespace PCM.Infrastructure.Security;

/// <summary>Role names, identical to the rows seeded in dbo.AspNetRoles.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Seller = "Seller";
    public const string Customer = "Customer";
}

/// <summary>
/// Claim types written into the JWT (short JWT names, no XML-namespace mapping).
/// The UI uses the same names for its cookie principal.
/// </summary>
public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";

    /// <summary>Seller status at token issue time. Display only: authorization re-checks the database.</summary>
    public const string SellerStatus = "seller_status";
}

/// <summary>Authorization policy names used by the API controllers.</summary>
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string CustomerOnly = nameof(CustomerOnly);

    /// <summary>Any seller, whatever the approval status (profile, status page).</summary>
    public const string SellerAny = nameof(SellerAny);

    /// <summary>Seller whose profile is Approved right now (offers, inventory, orders from Phase 4).</summary>
    public const string ApprovedSeller = nameof(ApprovedSeller);

    /// <summary>
    /// Approved or Suspended seller: may read and move their existing sub-orders. Suspension stops new
    /// sales (offers are hidden), not the duty to ship or cancel what customers already ordered.
    /// </summary>
    public const string SellerCanFulfil = nameof(SellerCanFulfil);
}
