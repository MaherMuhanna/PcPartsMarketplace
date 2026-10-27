using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Sellers;

/// <summary>Seller as seen by an admin, including the owner's account details.</summary>
public sealed record SellerAdminDto(
    int Id,
    int UserId,
    string OwnerFullName,
    string OwnerEmail,
    string StoreName,
    string? Description,
    string? ContactPhone,
    string Status,
    string? StatusReason,
    DateTime? ApprovedAt,
    DateTime CreatedAt);

/// <summary>GET /api/v1/admin/sellers query. Status: Pending / Approved / Rejected / Suspended. Sort: newest (default), oldest, storeName.</summary>
public sealed class SellerListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "oldest", "storeName"];

    public string? Status { get; init; }
}

/// <summary>Body of POST /api/v1/admin/sellers/{id}/reject and /suspend.</summary>
public sealed record SellerStatusReasonRequest
{
    public string Reason { get; init; } = string.Empty;
}

/// <summary>The signed-in seller's own profile (GET /api/v1/seller/profile).</summary>
public sealed record SellerProfileDto(
    int Id,
    string StoreName,
    string? Description,
    string? ContactPhone,
    string Status,
    string? StatusReason,
    DateTime? ApprovedAt,
    DateTime CreatedAt);

/// <summary>PUT /api/v1/seller/profile. The store name is fixed after registration.</summary>
public sealed record UpdateSellerProfileRequest
{
    public string? Description { get; init; }

    public string? ContactPhone { get; init; }
}
