using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PCM.UI.ApiClients;
using PCM.UI.Filters;
using PCM.UI.Http;
using PCM.UI.Security;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllersWithViews(options =>
    {
        // Every POST form is protected against CSRF (tag helpers add the token automatically).
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<ApiExceptionFilter>();
    });

    // ---- Cookie session that carries the API's JWT + refresh token (see AuthSession) ----
    builder.Services.AddAuthentication(AuthSession.Scheme)
        .AddCookie(AuthSession.Scheme, options =>
        {
            options.Cookie.Name = "pcm.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Docker serves plain HTTP
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = false;             // lifetime follows the refresh token
        });
    builder.Services.AddAuthorization();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton(TimeProvider.System);

    // Every API call carries the browser's IP (X-Forwarded-For), so the API rate-limits per user.
    builder.Services.AddTransient<ClientIpForwardingHandler>();
    builder.Services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<ClientIpForwardingHandler>());

    // ---- Typed API clients via IHttpClientFactory: pooled handlers, no `new HttpClient()` per request ----
    var apiBaseUrl = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is missing."));

    void ConfigureApiClient(HttpClient client)
    {
        client.BaseAddress = apiBaseUrl;
        client.Timeout = TimeSpan.FromSeconds(15);
    }

    builder.Services.AddTransient<ApiTokenHandler>();
    builder.Services.AddHttpClient(ApiTokenHandler.RefreshClientName, ConfigureApiClient);

    builder.Services.AddHttpClient<ICatalogApiClient, CatalogApiClient>(ConfigureApiClient);
    builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<IAddressesApiClient, AddressesApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<IAdminSellersApiClient, AdminSellersApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<ISellerProfileApiClient, SellerProfileApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<ISellerOffersApiClient, SellerOffersApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<ICartApiClient, CartApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<IOrdersApiClient, OrdersApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();
    builder.Services.AddHttpClient<ISellerOrdersApiClient, SellerOrdersApiClient>(ConfigureApiClient).AddHttpMessageHandler<ApiTokenHandler>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    {
        app.UseHttpsRedirection();
    }

    app.UseSerilogRequestLogging();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "PCM.UI terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
