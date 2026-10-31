using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace PCM.API.Security;

/// <summary>
/// Real client IP behind the MVC UI. Every browser request reaches the API through the UI server, so
/// without this the rate limiter and RefreshToken.CreatedByIp would see one IP (the UI's) for all users.
/// The UI sends X-Forwarded-For with the browser's IP; the API accepts that header only from trusted
/// proxies, so a client calling the API directly cannot fake its IP.
/// </summary>
public static class ForwardedHeadersSetup
{
    /// <summary>Configuration section: <c>ForwardedHeaders:KnownProxies</c> = list of trusted proxy IPs.</summary>
    public const string KnownProxiesKey = "ForwardedHeaders:KnownProxies";

    public static WebApplicationBuilder AddTrustedProxies(this WebApplicationBuilder builder)
    {
        var configured = builder.Configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [];

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only the client IP. Scheme and host are not forwarded (the UI calls the API by its own URL).
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

            // One hop: the UI. Anything the browser put in front of that is ignored.
            options.ForwardLimit = 1;

            // Defaults already trust loopback (UI and API on the same machine, e.g. Visual Studio).
            // Docker adds the UI container's fixed IP through configuration (see docker-compose.yml).
            foreach (var value in configured)
            {
                if (!IPAddress.TryParse(value, out var address))
                {
                    throw new InvalidOperationException($"'{value}' in {KnownProxiesKey} is not a valid IP address.");
                }

                options.KnownProxies.Add(address);
            }
        });

        return builder;
    }
}
