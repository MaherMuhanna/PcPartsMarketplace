using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

public sealed class SellerProfileService(IUnitOfWork unitOfWork) : ISellerProfileService
{
    public async Task<SellerProfileDto> GetMyProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        (await GetOrThrowAsync(userId, cancellationToken)).ToDto();

    public async Task<SellerProfileDto> UpdateMyProfileAsync(int userId, UpdateSellerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await GetOrThrowAsync(userId, cancellationToken);

        // Store name is the identity the admin approved, so only these fields are editable.
        profile.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        profile.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.ToDto();
    }

    private async Task<SellerProfile> GetOrThrowAsync(int userId, CancellationToken cancellationToken) =>
        await unitOfWork.SellerProfiles.GetByUserIdAsync(userId, cancellationToken)
        ?? throw new NotFoundException("Seller profile not found.");
}
