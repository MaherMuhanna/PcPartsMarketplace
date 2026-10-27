using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Brands;

/// <summary>Brand as returned by the API.</summary>
public sealed record BrandDto(int Id, string Name, string? LogoUrl);

/// <summary>GET /api/v1/brands query. Sort: name (default), -name, newest.</summary>
public sealed class BrandListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["name", "-name", "newest"];
}
