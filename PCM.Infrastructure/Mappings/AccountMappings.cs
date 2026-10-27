using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.Infrastructure.Mappings;

/// <summary>Manual mappings for addresses and seller profiles.</summary>
public static class AccountMappings
{
    public static AddressDto ToDto(this Address address) =>
        new(address.Id, address.City, address.Street, address.Details, address.Phone, address.IsDefault);

    public static SellerProfileDto ToDto(this SellerProfile profile) =>
        new(profile.Id, profile.StoreName, profile.Description, profile.ContactPhone,
            profile.Status, profile.StatusReason, profile.ApprovedAt, profile.CreatedAt);

    /// <summary>Owner name and email come from AspNetUsers (a different DbContext), hence passed in.</summary>
    public static SellerAdminDto ToAdminDto(this SellerProfile profile, string ownerFullName, string ownerEmail) =>
        new(profile.Id, profile.UserId, ownerFullName, ownerEmail, profile.StoreName, profile.Description,
            profile.ContactPhone, profile.Status, profile.StatusReason, profile.ApprovedAt, profile.CreatedAt);
}
