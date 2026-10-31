using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PCM.Infrastructure.DTO.Auth;
using PCM.UI.Security;

namespace PCM.UI.Tests;

/// <summary>
/// The handler every typed API client goes through: bearer token, refresh shortly before expiry,
/// one refresh-and-retry after a 401, and sign-out when the refresh is refused.
/// </summary>
public sealed class ApiTokenHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Uri ApiBase = new("https://api.test/");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------

    [Fact]
    public async Task Valid_token_is_sent_as_bearer_without_refresh()
    {
        var test = await TestSetup.SignedInAsync(accessExpiresAt: Now.AddMinutes(30));

        var response = await test.SendAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer access-1", Assert.Single(test.Api.Requests).Authorization);
        Assert.Empty(test.Refresh.Requests);
    }

    [Fact]
    public async Task Token_expiring_within_a_minute_is_refreshed_before_the_call()
    {
        var test = await TestSetup.SignedInAsync(accessExpiresAt: Now.AddSeconds(30));
        test.Refresh.Respond(_ => Json(NewAuth("access-2", "refresh-2")));

        await test.SendAsync();

        var refresh = Assert.Single(test.Refresh.Requests);
        Assert.Equal("refresh-1", refresh.Body!.RefreshToken);
        Assert.Equal("Bearer access-2", Assert.Single(test.Api.Requests).Authorization);

        // The cookie is re-issued with the new pair, and later calls in the request use it.
        Assert.Equal(1, test.Auth.SignIns);
        Assert.Equal("refresh-2", (await AuthSession.GetTokensAsync(test.HttpContext))!.RefreshToken);
    }

    [Fact]
    public async Task Unauthorized_response_triggers_one_refresh_and_retry()
    {
        var test = await TestSetup.SignedInAsync(accessExpiresAt: Now.AddMinutes(30));
        test.Api.Respond(request => request.Authorization == "Bearer access-1"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK));
        test.Refresh.Respond(_ => Json(NewAuth("access-2", "refresh-2")));

        var response = await test.SendAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "Bearer access-1", "Bearer access-2" }, test.Api.Requests.Select(r => r.Authorization));
        Assert.Single(test.Refresh.Requests);
    }

    [Fact]
    public async Task Second_unauthorized_is_returned_without_another_refresh()
    {
        var test = await TestSetup.SignedInAsync(accessExpiresAt: Now.AddMinutes(30));
        test.Api.Respond(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        test.Refresh.Respond(_ => Json(NewAuth("access-2", "refresh-2")));

        var response = await test.SendAsync();

        // The filter then sends the user to the login page; the handler never loops.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, test.Api.Requests.Count);
        Assert.Single(test.Refresh.Requests);
    }

    [Fact]
    public async Task Refused_refresh_signs_the_user_out()
    {
        var test = await TestSetup.SignedInAsync(accessExpiresAt: Now.AddSeconds(10));
        test.Refresh.Respond(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await test.SendAsync();

        Assert.Equal(1, test.Auth.SignOuts);
        Assert.Null(await AuthSession.GetTokensAsync(test.HttpContext));
        // The call still goes out, without a token: the API answers 401 and the UI shows the login page.
        Assert.Null(Assert.Single(test.Api.Requests).Authorization);
    }

    [Fact]
    public async Task Anonymous_requests_pass_through_untouched()
    {
        var test = TestSetup.Anonymous();

        await test.SendAsync();

        Assert.Null(Assert.Single(test.Api.Requests).Authorization);
        Assert.Empty(test.Refresh.Requests);
    }

    // ---------------------------------------------------------------------

    private static AuthResponse NewAuth(string accessToken, string refreshToken, DateTime? accessExpiresAt = null) => new(
        accessToken,
        accessExpiresAt ?? Now.AddMinutes(60),
        refreshToken,
        Now.AddDays(30),
        new UserDto(7, "Test Customer", "customer@test.local", ["Customer"], null));

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>A request the stub received: its Authorization header and, for refresh calls, the body.</summary>
    private sealed record RecordedRequest(string? Authorization, RefreshTokenRequest? Body);

    /// <summary>Terminal handler that records requests and answers with a configurable response (200 by default).</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private Func<RecordedRequest, HttpResponseMessage> _respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public List<RecordedRequest> Requests { get; } = [];

        public void Respond(Func<RecordedRequest, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadFromJsonAsync<RefreshTokenRequest>(cancellationToken);
            var recorded = new RecordedRequest(request.Headers.Authorization?.ToString(), body);
            Requests.Add(recorded);
            return _respond(recorded);
        }
    }

    /// <summary>Counts cookie sign-ins and sign-outs; there is no incoming cookie (the session lives in HttpContext.Items).</summary>
    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public int SignIns { get; private set; }

        public int SignOuts { get; private set; }

        /// <summary>Forgets the sign-in made while arranging the test.</summary>
        public void Reset() => SignIns = SignOuts = 0;

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignIns++;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOuts++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    /// <summary>One handler under test with its HttpContext, stub API and stub refresh endpoint.</summary>
    private sealed class TestSetup
    {
        public required HttpContext HttpContext { get; init; }

        public required FakeAuthenticationService Auth { get; init; }

        public StubHandler Api { get; } = new();

        public StubHandler Refresh { get; } = new();

        public static TestSetup Anonymous()
        {
            var auth = new FakeAuthenticationService();
            var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
            return new TestSetup { HttpContext = new DefaultHttpContext { RequestServices = services }, Auth = auth };
        }

        /// <summary>A signed-in session holding access-1 / refresh-1.</summary>
        public static async Task<TestSetup> SignedInAsync(DateTime accessExpiresAt)
        {
            var test = Anonymous();
            await AuthSession.SignInAsync(test.HttpContext, NewAuth("access-1", "refresh-1", accessExpiresAt), isPersistent: false);
            test.Auth.Reset();
            return test;
        }

        public async Task<HttpResponseMessage> SendAsync()
        {
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(HttpContext);

            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(ApiTokenHandler.RefreshClientName).Returns(_ => new HttpClient(Refresh, disposeHandler: false) { BaseAddress = ApiBase });

            var handler = new ApiTokenHandler(accessor, factory, new FixedTimeProvider(Now), NullLogger<ApiTokenHandler>.Instance)
            {
                InnerHandler = Api
            };

            using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiBase, "api/v1/orders"));
            return await invoker.SendAsync(request, Ct);
        }
    }
}
