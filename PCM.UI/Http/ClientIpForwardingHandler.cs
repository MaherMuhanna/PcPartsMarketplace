namespace PCM.UI.Http;

/// <summary>
/// Adds the browser's IP to every API call as X-Forwarded-For, so the API rate-limits and audits
/// per user instead of per UI server. The API trusts this header only from known proxies
/// (ForwardedHeaders:KnownProxies in PCM.API).
/// </summary>
/// <remarks>
/// The value is taken from the connection, never copied from an incoming X-Forwarded-For header,
/// because a browser can send any value there. If the UI itself is ever put behind a reverse proxy,
/// enable UseForwardedHeaders in the UI for that proxy first, so RemoteIpAddress is the real client.
/// </remarks>
public sealed class ClientIpForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string HeaderName = "X-Forwarded-For";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);

        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (clientIp is not null)
        {
            // IPv4 clients on a dual-stack socket show up as ::ffff:a.b.c.d; send the plain IPv4 form.
            var value = clientIp.IsIPv4MappedToIPv6 ? clientIp.MapToIPv4().ToString() : clientIp.ToString();
            request.Headers.TryAddWithoutValidation(HeaderName, value);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
