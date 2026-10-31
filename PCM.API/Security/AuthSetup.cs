using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Security;

namespace PCM.API.Security;

/// <summary>JWT authentication, authorization policies and the auth rate limiter, in one place.</summary>
public static class AuthSetup
{
    /// <summary>Rate-limiter policy applied to the anonymous /auth endpoints.</summary>
    public const string AuthRateLimitPolicy = "auth";

    public static WebApplicationBuilder AddApiAuth(this WebApplicationBuilder builder)
    {
        EnsureDevelopmentSigningKey(builder);

        // Validated at startup (not at Build), so EF design-time tools still work without a key.
        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IAccessTokenFactory, JwtAccessTokenFactory>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the short JWT claim names (sub, role, ...) instead of mapping them to XML namespaces.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtAccessTokenFactory.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaimTypes.Name,
                    RoleClaimType = AppClaimTypes.Role
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(Policies.CustomerOnly, policy => policy.RequireRole(Roles.Customer))
            .AddPolicy(Policies.SellerAny, policy => policy.RequireRole(Roles.Seller))
            .AddPolicy(Policies.ApprovedSeller, policy => policy
                .RequireRole(Roles.Seller)
                .AddRequirements(new SellerStatusRequirement(SellerStatus.Approved)))
            // A suspended seller can no longer sell, but still has to finish the sub-orders they accepted.
            .AddPolicy(Policies.SellerCanFulfil, policy => policy
                .RequireRole(Roles.Seller)
                .AddRequirements(new SellerStatusRequirement(SellerStatus.Approved, SellerStatus.Suspended)));
        builder.Services.AddScoped<IAuthorizationHandler, SellerStatusHandler>();

        AddAuthRateLimiter(builder);

        return builder;
    }

    /// <summary>
    /// Fixed window per client IP on login / register / refresh (agreed: 10 per minute;
    /// configurable through RateLimiting:AuthPermitPerMinute). Rejections are 429 ProblemDetails.
    /// </summary>
    private static void AddAuthRateLimiter(WebApplicationBuilder builder)
    {
        var permitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Too many attempts. Wait a minute and try again."
                    }
                });
            };
        });
    }

    /// <summary>
    /// Development convenience: without a configured key, generate a random one for this
    /// process (tokens then stop validating after a restart, and clients refresh).
    /// Set a stable key with user-secrets; outside Development the key is mandatory.
    /// </summary>
    private static void EnsureDevelopmentSigningKey(WebApplicationBuilder builder)
    {
        const string keyPath = JwtOptions.SectionName + ":SigningKey";

        if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration[keyPath]))
        {
            builder.Configuration[keyPath] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            Serilog.Log.Warning(
                "Jwt:SigningKey is not set; using a random key for this run. Set one with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 characters>\" --project PCM.API");
        }
    }
}
