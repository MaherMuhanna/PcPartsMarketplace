using PCM.Infrastructure.DTO.Catalog;

namespace PCM.UI.Models;

public sealed class HomeViewModel
{
    public IReadOnlyList<CategoryTreeDto> Categories { get; set; } = [];

    public IReadOnlyList<ProductListItemDto> NewestProducts { get; set; } = [];
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
