using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Seller home. Approved sellers see their numbers; any other status sees a store-status card
/// (what to expect next) instead of an "Access denied" page.
/// </summary>
[Authorize(Roles = Roles.Seller)]
[Route("seller")]
public sealed class SellerDashboardController(IDashboardApiClient dashboardApi, ISellerProfileApiClient profileApi) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SellerDashboardDto? dashboard = null;

        // The claim can be up to one access-token lifetime old; the API checks the live status.
        if (User.FindFirstValue(AppClaimTypes.SellerStatus) == "Approved")
        {
            try
            {
                dashboard = await dashboardApi.GetSellerDashboardAsync(cancellationToken);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                // Suspended (or otherwise no longer approved) since the token was issued: show the status card.
            }
        }

        var profile = await profileApi.GetProfileAsync(cancellationToken);
        return View(new SellerDashboardViewModel { Profile = profile, Dashboard = dashboard });
    }
}
