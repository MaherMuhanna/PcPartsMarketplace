using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;

namespace PCM.UI.Controllers;

/// <summary>Admin home: review queues, sub-orders per status and recent sales, each linking to its list.</summary>
[Authorize(Roles = Roles.Admin)]
[Route("admin")]
public sealed class AdminDashboardController(IDashboardApiClient dashboardApi) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await dashboardApi.GetAdminDashboardAsync(cancellationToken));
}
