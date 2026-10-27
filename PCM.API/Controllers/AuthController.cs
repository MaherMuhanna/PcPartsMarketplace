using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Registration, login and token lifecycle. Anonymous endpoints are rate limited per IP.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Registers a customer and signs them in.</summary>
    [HttpPost("register/customer")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> RegisterCustomer(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.RegisterCustomerAsync(request, ClientIp, cancellationToken);
        return CreatedAtAction(nameof(Me), response);
    }

    /// <summary>Registers a seller (status Pending until an admin approves) and signs them in.</summary>
    [HttpPost("register/seller")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> RegisterSeller(RegisterSellerRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.RegisterSellerAsync(request, ClientIp, cancellationToken);
        return CreatedAtAction(nameof(Me), response);
    }

    /// <summary>Signs in with email and password. 5 failures in a row lock the account for 15 minutes.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request, ClientIp, cancellationToken));

    /// <summary>Exchanges a refresh token for a new token pair. The old refresh token stops working.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.RefreshAsync(request.RefreshToken, ClientIp, cancellationToken));

    /// <summary>Revokes the given refresh token (signs this device out).</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(User.GetUserId(), request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>The signed-in user, with roles and seller status read fresh from the database.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken));

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
