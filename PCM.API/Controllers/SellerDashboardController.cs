using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>The signed-in seller's summary: sub-orders per status, stock health, sales and recent orders.</summary>
[ApiController]
[Route("api/v1/seller/dashboard")]
[Authorize(Policy = Policies.ApprovedSeller)]
public sealed class SellerDashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SellerDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerDashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetSellerDashboardAsync(User.GetUserId(), cancellationToken));
}
