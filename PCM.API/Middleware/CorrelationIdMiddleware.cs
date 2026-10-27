using Serilog.Context;

namespace PCM.API.Middleware;

/// <summary>
/// Gives every request a correlation id: taken from the incoming X-Correlation-Id
/// header (so the MVC UI can pass its own) or generated. The id is returned in the
/// response header, added to every log event of the request, and included in
/// ProblemDetails responses, so one id links a user-visible error to its logs.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        // Never trust arbitrary client input in logs: accept only short, simple ids.
        if (string.IsNullOrWhiteSpace(correlationId)
            || correlationId.Length > MaxLength
            || !correlationId.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ItemKey, correlationId))
        {
            await next(context);
        }
    }
}
