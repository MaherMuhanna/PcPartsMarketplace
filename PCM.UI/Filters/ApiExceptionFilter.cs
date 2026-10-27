using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PCM.UI.ApiClients;
using PCM.UI.Security;

namespace PCM.UI.Filters;

/// <summary>
/// Turns API errors that a controller did not handle into the right page:
/// 401 = session is over, go to login; 403 = access denied page; 404 = not found page;
/// API unreachable = friendly error. Anything else falls through to the error page.
/// </summary>
public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiException { StatusCode: HttpStatusCode.Unauthorized }:
                await AuthSession.SignOutAsync(context.HttpContext);
                var request = context.HttpContext.Request;
                var returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                context.ExceptionHandled = true;
                break;

            case ApiException { StatusCode: HttpStatusCode.Forbidden }:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                context.ExceptionHandled = true;
                break;

            case ApiException { StatusCode: HttpStatusCode.NotFound }:
                context.Result = new ViewResult { ViewName = "NotFound", StatusCode = StatusCodes.Status404NotFound };
                context.ExceptionHandled = true;
                break;

            case HttpRequestException ex:
                logger.LogWarning(ex, "The API is not reachable");
                context.Result = new ViewResult { ViewName = "ApiUnavailable", StatusCode = StatusCodes.Status503ServiceUnavailable };
                context.ExceptionHandled = true;
                break;

            // HttpClient.Timeout (15 s) elapsed: the API is up but too slow, or paused in the debugger.
            // Not the browser closing the tab: that cancels RequestAborted, and then there is no one to answer.
            case TaskCanceledException ex when !context.HttpContext.RequestAborted.IsCancellationRequested:
                logger.LogWarning(ex, "The API did not answer in time");
                context.Result = new ViewResult { ViewName = "ApiUnavailable", StatusCode = StatusCodes.Status503ServiceUnavailable };
                context.ExceptionHandled = true;
                break;
        }
    }
}
