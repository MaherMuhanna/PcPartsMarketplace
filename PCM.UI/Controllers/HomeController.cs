using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Catalog;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>Landing page: shop by category and the newest products.</summary>
public sealed class HomeController(ICatalogApiClient catalogApi) : Controller
{
    private const int NewestCount = 8;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await catalogApi.GetCategoriesAsync(cancellationToken);
        var newest = await catalogApi.SearchAsync($"sort=newest&pageSize={NewestCount}", cancellationToken);

        return View(new HomeViewModel
        {
            // Shop-by-category tiles show the leaves (CPU, GPU, ...), the places products live.
            Categories = Flatten(categories).Where(c => c.IsLeaf).ToList(),
            NewestProducts = newest.Items
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    private static IEnumerable<CategoryTreeDto> Flatten(IEnumerable<CategoryTreeDto> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
}
