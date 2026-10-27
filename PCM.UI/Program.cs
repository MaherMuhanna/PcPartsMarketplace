using PCM.UI.ApiClients;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllersWithViews();

    // Typed API clients via IHttpClientFactory: pooled handlers, no `new HttpClient()` per request.
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is missing.");

    builder.Services.AddHttpClient<IBrandsApiClient, BrandsApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(15);
    });

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
