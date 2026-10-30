using PCM.API.Filters;
using PCM.API.Middleware;
using PCM.API.OpenApi;
using PCM.API.Security;
using PCM.DomainEntities.Context;
using PCM.Infrastructure;
using PCM.Infrastructure.Seeding;
using PCM.Repositories;
using Scalar.AspNetCore;
using Serilog;

// Minimal console logger so startup failures (e.g. missing connection string) are visible.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ---- Logging: Serilog configured from appsettings (Console + daily rolling file) ----
    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---- Data access + business services ----
    var connectionString = builder.Configuration.GetConnectionString("PcParts")
        ?? throw new InvalidOperationException(
            "Connection string 'PcParts' is missing. Set it with: " +
            "dotnet user-secrets set \"ConnectionStrings:PcParts\" \"<connection string>\" --project PCM.API");

    builder.Services
        .AddDataAccess(connectionString)
        .AddInfrastructure();

    // ---- Identity + JWT bearer auth, role policies, rate limiting on /auth ----
    builder.AddApiAuth();

    // ---- Web API ----
    builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());

    // RFC 9457 ProblemDetails for every error, enriched with trace and correlation ids.
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // OpenAPI document (/openapi/v1.json) with the JWT bearer scheme, rendered by Scalar at /scalar.
    builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<PcPartsContext>("database");

    var app = builder.Build();

    // ---- HTTP pipeline (order matters) ----
    app.UseExceptionHandler();              // unhandled exceptions -> ProblemDetails
    app.UseStatusCodePages();               // empty 4xx/5xx responses -> ProblemDetails
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();         // one structured log line per request

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();                   // /openapi/v1.json
        app.MapScalarApiReference(options => options   // /scalar
            .WithTitle("PC Parts Marketplace API")
            .AddPreferredSecuritySchemes(BearerSecuritySchemeTransformer.SchemeName));
    }

    // Inside Docker the API serves plain HTTP behind the compose network.
    if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // Demo accounts, product proposals and offers for local runs and Docker (Development only; see README).
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:DemoUsers"))
    {
        await SeedDemoUsersAsync(app);
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException) // HostAbortedException = normal stop by EF design-time tools
{
    Log.Fatal(ex, "PCM.API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task SeedDemoUsersAsync(WebApplication app)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoUserSeeder>();
        await seeder.SeedAsync(app.Configuration["Seed:DemoPassword"] ?? string.Empty);

        // Needs seller1 (created just above) and the catalog from Database/seed.sql.
        await scope.ServiceProvider.GetRequiredService<DemoCatalogSeeder>().SeedAsync();

        // Needs the approved demo sellers (seller1, seller3) and the published seed products.
        await scope.ServiceProvider.GetRequiredService<DemoOfferSeeder>().SeedAsync();
    }
    catch (Exception ex)
    {
        // A missing database should not stop the API from starting; /health will report it.
        Log.Error(ex, "Seeding demo data failed");
    }
}

/// <summary>Exposed for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
