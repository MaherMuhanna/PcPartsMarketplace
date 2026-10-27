using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Extensions;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>My addresses (customers). Ownership is enforced by the API; the UI only shows what it returns.</summary>
[Authorize(Roles = Roles.Customer)]
public sealed class AddressesController(IAddressesApiClient addressesApi) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var page = await addressesApi.GetAddressesAsync(cancellationToken: cancellationToken);
        return View(new AddressListViewModel { Addresses = page.Items });
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new AddressFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(AddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        try
        {
            await addressesApi.CreateAddressAsync(new CreateAddressRequest
            {
                City = model.City,
                Street = model.Street,
                Details = model.Details,
                Phone = model.Phone,
                IsDefault = model.IsDefault
            }, cancellationToken);

            TempData["Success"] = "Address added.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (ModelState.TryAddApiError(ex))
        {
            return View("Form", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken) =>
        View("Form", AddressFormViewModel.From(await addressesApi.GetAddressAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AddressFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        try
        {
            await addressesApi.UpdateAddressAsync(id, new UpdateAddressRequest
            {
                City = model.City,
                Street = model.Street,
                Details = model.Details,
                Phone = model.Phone
            }, cancellationToken);

            TempData["Success"] = "Address updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (ModelState.TryAddApiError(ex))
        {
            return View("Form", model);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await addressesApi.DeleteAddressAsync(id, cancellationToken);
        TempData["Success"] = "Address deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetDefault(int id, CancellationToken cancellationToken)
    {
        await addressesApi.SetDefaultAsync(id, cancellationToken);
        TempData["Success"] = "Default address changed.";
        return RedirectToAction(nameof(Index));
    }
}
