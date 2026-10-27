using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Auth;
using PCM.UI.ApiClients;
using PCM.UI.Extensions;
using PCM.UI.Models;
using PCM.UI.Security;

namespace PCM.UI.Controllers;

/// <summary>Login, registration and logout. The UI never sees passwords after posting them to the API.</summary>
public sealed class AccountController(IAuthApiClient authApi, ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = await authApi.LoginAsync(new LoginRequest { Email = model.Email, Password = model.Password }, cancellationToken);
            await AuthSession.SignInAsync(HttpContext, auth, model.RememberMe);
            return RedirectToLocal(model.ReturnUrl);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Wrong password, unknown email or locked account: show the API's message.
            ModelState.AddModelError(string.Empty, ex.UserMessage);
        }
        catch (ApiException ex) when (ModelState.TryAddApiError(ex))
        {
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(AccountType type = AccountType.Customer) =>
        View(new RegisterViewModel { AccountType = type });

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = model.AccountType == AccountType.Seller
                ? await authApi.RegisterSellerAsync(new RegisterSellerRequest
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password,
                    StoreName = model.StoreName ?? string.Empty,
                    Description = model.Description,
                    ContactPhone = model.ContactPhone
                }, cancellationToken)
                : await authApi.RegisterCustomerAsync(new RegisterCustomerRequest
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password
                }, cancellationToken);

            await AuthSession.SignInAsync(HttpContext, auth, isPersistent: false);

            TempData["Success"] = model.AccountType == AccountType.Seller
                ? "Your seller account was created. An admin will review it soon."
                : "Welcome! Your account was created.";

            return model.AccountType == AccountType.Seller
                ? RedirectToAction("Index", "Store")
                : RedirectToAction("Index", "Home");
        }
        catch (ApiException ex) when (ModelState.TryAddApiError(ex))
        {
            return View(model);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var tokens = await AuthSession.GetTokensAsync(HttpContext);
        if (tokens is not null)
        {
            try
            {
                // Revoke the refresh token on the API; the cookie is cleared either way.
                await authApi.LogoutAsync(tokens.RefreshToken, cancellationToken);
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException)
            {
                logger.LogInformation(ex, "API logout failed; clearing the local session anyway");
            }
        }

        await AuthSession.SignOutAsync(HttpContext);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
