using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Extensions;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>"My store" for sellers in any status: approval status, reason, and editable details.</summary>
[Authorize(Roles = Roles.Seller)]
public sealed class StoreController(ISellerProfileApiClient profileApi) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await profileApi.GetProfileAsync(cancellationToken);
        return View(new StoreViewModel
        {
            Profile = profile,
            Description = profile.Description,
            ContactPhone = profile.ContactPhone
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(StoreViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await profileApi.UpdateProfileAsync(new UpdateSellerProfileRequest
                {
                    Description = model.Description,
                    ContactPhone = model.ContactPhone
                }, cancellationToken);

                TempData["Success"] = "Store details saved.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ModelState.TryAddApiError(ex))
            {
            }
        }

        model.Profile = await profileApi.GetProfileAsync(cancellationToken);
        return View(model);
    }
}
