using PCM.API.Filters;
using PCM.API.Middleware;
using PCM.DomainEntities.Context;
using PCM.Infrastructure;
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

    builder.Services.AddOpenApi();

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
        app.MapScalarApiReference();        // /scalar
    }

    // Inside Docker the API serves plain HTTP behind the compose network.
    if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    {
        app.UseHttpsRedirection();
    }

    app.MapControllers();
    app.MapHealthChecks("/health");

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

/// <summary>Exposed for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
