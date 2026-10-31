using System.Net;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PCM.UI.ApiClients;

namespace PCM.UI.Extensions;

public static class ModelStateExtensions
{
    /// <summary>
    /// Copies an API error into ModelState so the form shows it: 400 field errors go next
    /// to their inputs (camelCase API names mapped to PascalCase view-model names), any
    /// other error becomes a form-level message. Returns false for errors the form cannot
    /// show (401/403/404), which the caller should rethrow for <see cref="Filters.ApiExceptionFilter"/>.
    /// </summary>
    public static bool TryAddApiError(this ModelStateDictionary modelState, ApiException exception)
    {
        if (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            return false;
        }

        if (exception.StatusCode == HttpStatusCode.BadRequest && exception.Problem?.Errors is { Count: > 0 } errors)
        {
            foreach (var (field, messages) in errors)
            {
                var key = field.Length == 0 ? string.Empty : char.ToUpperInvariant(field[0]) + field[1..];
                foreach (var message in messages)
                {
                    modelState.AddModelError(key, message);
                }
            }

            return true;
        }

        modelState.AddModelError(string.Empty, exception.StatusCode == HttpStatusCode.TooManyRequests
            ? "Too many attempts. Wait a minute and try again."
            : exception.UserMessage);
        return true;
    }

    /// <summary>
    /// Messages of <paramref name="key"/> and of its sub-keys ("Specs[2]" also returns "Specs[2].OptionId",
    /// but not "Specs[20]"). Keys are compared ignoring case, like ModelState itself.
    /// For list rows whose errors may be attached to the row or to one of its fields.
    /// </summary>
    public static IReadOnlyList<string> ErrorsFor(this ModelStateDictionary modelState, string key)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        return [.. modelState
            .Where(entry => entry.Value is not null)
            .Where(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                || (entry.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase) && entry.Key.Length > key.Length && entry.Key[key.Length] == '.'))
            .SelectMany(entry => entry.Value!.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrEmpty(message))
            .Distinct()];
    }
}
