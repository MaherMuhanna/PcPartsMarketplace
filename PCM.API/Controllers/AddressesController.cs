using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// The signed-in customer's shipping addresses. The route has no user id: the owner
/// always comes from the token, and someone else's address id returns 404.
/// </summary>
[ApiController]
[Route("api/v1/account/addresses")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class AddressesController(IAddressService addressService) : ControllerBase
{
    /// <summary>Lists the caller's addresses: default first, then newest.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<AddressDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AddressDto>>> GetAddresses([FromQuery] AddressListQuery query, CancellationToken cancellationToken) =>
        Ok(await addressService.GetAddressesAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddressDto>> GetAddress(int id, CancellationToken cancellationToken) =>
        Ok(await addressService.GetAddressAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>Adds an address. The first one always becomes the default.</summary>
    [HttpPost]
    [ProducesResponseType<AddressDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AddressDto>> CreateAddress(CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await addressService.CreateAddressAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetAddress), new { id = address.Id }, address);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddressDto>> UpdateAddress(int id, UpdateAddressRequest request, CancellationToken cancellationToken) =>
        Ok(await addressService.UpdateAddressAsync(User.GetUserId(), id, request, cancellationToken));

    /// <summary>Deletes the address. If it was the default, the newest remaining one becomes the default.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken cancellationToken)
    {
        await addressService.DeleteAddressAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Makes the address the default one.</summary>
    [HttpPut("{id:int}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(int id, CancellationToken cancellationToken)
    {
        await addressService.SetDefaultAddressAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
