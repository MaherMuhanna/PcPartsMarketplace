using PCM.Infrastructure.DTO.Brands;

namespace PCM.UI.Models;

public sealed class HomeViewModel
{
    public IReadOnlyList<BrandDto> Brands { get; set; } = [];

    public string? ApiError { get; set; }
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
