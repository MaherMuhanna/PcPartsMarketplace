using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Marketplace summary for the admin home page (review queues, sub-orders per status, sales).</summary>
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminDashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetAdminDashboardAsync(cancellationToken));
}
