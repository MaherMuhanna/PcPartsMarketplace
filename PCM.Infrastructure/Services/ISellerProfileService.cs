using PCM.Infrastructure.DTO.Sellers;

namespace PCM.Infrastructure.Services;

/// <summary>The signed-in seller's own store profile (available in any status).</summary>
public interface ISellerProfileService
{
    Task<SellerProfileDto> GetMyProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<SellerProfileDto> UpdateMyProfileAsync(int userId, UpdateSellerProfileRequest request, CancellationToken cancellationToken = default);
}
