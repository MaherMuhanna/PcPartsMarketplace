using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Brands;

/// <summary>Brand as returned by the API. The slug is the value of the catalog filter ?brand=.</summary>
public sealed record BrandDto(int Id, string Name, string Slug, string? LogoUrl);

/// <summary>GET /api/v1/brands query. Sort: name (default), -name, newest.</summary>
public sealed class BrandListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["name", "-name", "newest"];
}

/// <summary>POST and PUT /api/v1/admin/brands. The slug is generated from the name.</summary>
public sealed record BrandRequest
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional absolute http(s) URL of the logo.</summary>
    public string? LogoUrl { get; init; }
}
