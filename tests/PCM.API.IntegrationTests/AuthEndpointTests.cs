using System.Net;
using System.Net.Http.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Security;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class AuthEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_customer_returns_tokens_and_the_customer_role()
    {
        var auth = await factory.RegisterCustomerAsync();

        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.True(auth.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(55), "Access token should live about 60 minutes.");
        Assert.True(auth.RefreshTokenExpiresAt > DateTime.UtcNow.AddDays(29), "Refresh token should live about 30 days.");
        Assert.Equal(new[] { Roles.Customer }, auth.User.Roles);
        Assert.Null(auth.User.SellerStatus);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        var email = TestAuth.NewEmail();
        await factory.RegisterCustomerAsync(email);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register/customer", new RegisterCustomerRequest
        {
            FullName = "Someone Else",
            Email = email.ToUpperInvariant(),   // emails are case-insensitive
            Password = TestAuth.Password
        }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_a_weak_password_returns_400_with_the_field()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register/customer", new RegisterCustomerRequest
        {
            FullName = "Weak Password",
            Email = TestAuth.NewEmail(),
            Password = "password"
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"password\"", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Register_seller_starts_pending_and_can_sign_in()
    {
        var auth = await factory.RegisterSellerAsync();
        Assert.Equal("Pending", auth.User.SellerStatus);

        // Agreed rule: a pending seller can sign in and see their profile.
        var profile = await factory.CreateClient(auth).GetFromJsonAsync<SellerProfileDto>("/api/v1/seller/profile", Ct);
        Assert.Equal("Pending", profile!.Status);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        var email = TestAuth.NewEmail();
        await factory.RegisterCustomerAsync(email);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = "Wrong@12345" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account_even_for_the_right_password()
    {
        var email = TestAuth.NewEmail();
        await factory.RegisterCustomerAsync(email);
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = "Wrong@12345" }, Ct);
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = TestAuth.Password }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("locked", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Me_requires_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var auth = await factory.RegisterCustomerAsync();

        var me = await factory.CreateClient(auth).GetFromJsonAsync<UserDto>("/api/v1/auth/me", Ct);

        Assert.Equal(auth.User.Id, me!.Id);
        Assert.Equal(auth.User.Email, me.Email);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_stops_working()
    {
        var first = await factory.RegisterCustomerAsync();

        var second = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var rotated = (await second.Content.ReadFromJsonAsync<AuthResponse>(Ct))!;
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);

        var again = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_chain()
    {
        var first = await factory.RegisterCustomerAsync();
        var second = (await (await RefreshAsync(first.RefreshToken)).Content.ReadFromJsonAsync<AuthResponse>(Ct))!;
        var third = (await (await RefreshAsync(second.RefreshToken)).Content.ReadFromJsonAsync<AuthResponse>(Ct))!;

        // An attacker replays the very first token...
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first.RefreshToken)).StatusCode);

        // ...so the newest legitimate token is revoked too, forcing a fresh sign-in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(third.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var auth = await factory.RegisterCustomerAsync();

        var logout = await factory.CreateClient(auth).PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest { RefreshToken = auth.RefreshToken }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_returns_401()
    {
        var response = await RefreshAsync("not-a-real-token");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken }, Ct);
}
