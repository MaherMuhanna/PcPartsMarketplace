using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;

namespace PCM.UI.ViewComponents;

/// <summary>
/// Cart link with the number of units, shown in the navigation bar for customers.
/// A failing API never breaks the page: the link is then shown without a number.
/// </summary>
public sealed class CartBadgeViewComponent(ICartApiClient cartApi, ILogger<CartBadgeViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.IsInRole(Roles.Customer))
        {
            return Content(string.Empty);
        }

        int? count = null;
        try
        {
            count = (await cartApi.GetSummaryAsync(HttpContext.RequestAborted)).ItemCount;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(ex, "Cart badge: the cart summary could not be loaded");
        }

        return View(count);
    }
}
