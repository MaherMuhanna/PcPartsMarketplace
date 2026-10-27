using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PCM.API.Filters;

/// <summary>
/// Runs the FluentValidation validator registered for each action argument
/// (body or query object) before the action executes. Invalid input short-circuits
/// with a 400 ValidationProblemDetails whose keys are the camelCase field names
/// the client sent, e.g. { "errors": { "pageSize": ["..."] } }.
/// Controllers therefore only ever receive valid input.
/// </summary>
public sealed class FluentValidationFilter(IServiceProvider serviceProvider, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new ModelStateDictionary();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                errors.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName), failure.ErrorMessage);
            }
        }

        if (errors.ErrorCount > 0)
        {
            var problem = problemDetailsFactory.CreateValidationProblemDetails(
                context.HttpContext, errors, StatusCodes.Status400BadRequest);

            context.Result = new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" }
            };
            return;
        }

        await next();
    }
}
