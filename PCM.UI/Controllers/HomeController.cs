using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Phase 1 placeholder home page: proves the UI reaches the API through a typed client.
/// The real catalog pages arrive in Phase 6.
/// </summary>
public sealed class HomeController(IBrandsApiClient brandsApi, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new HomeViewModel();

        try
        {
            var brands = await brandsApi.GetBrandsAsync(cancellationToken: cancellationToken);
            model.Brands = brands?.Items ?? [];
        }
        catch (HttpRequestException ex)
        {
            // The UI stays up when the API is down; show a friendly message instead of a crash.
            logger.LogWarning(ex, "Could not load brands from the API");
            model.ApiError = "The API is not reachable. Make sure PCM.API is running.";
        }

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
