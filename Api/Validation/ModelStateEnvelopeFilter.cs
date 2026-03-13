using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DemoAssetDotnetApi.Api.Validation;

/// <summary>
/// Captures ModelState errors for consistent envelope formatting.
/// </summary>
public sealed class ModelStateEnvelopeFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            // REQ: REQ-004 - Convert binding errors into standardized validation envelope.
            var errors = new List<ValidationErrorDetail>();

            foreach (var (key, entry) in context.ModelState)
            {
                foreach (var err in entry.Errors)
                {
                    errors.Add(new ValidationErrorDetail
                    {
                        Code = "MODEL_BINDING_ERROR",
                        Tab = "Unknown",
                        FieldPath = key,
                        Message = string.IsNullOrWhiteSpace(err.ErrorMessage) ? "Invalid value." : err.ErrorMessage,
                        Severity = "Error"
                    });
                }
            }

            context.HttpContext.Items["__HasModelStateErrors"] = true;
            context.HttpContext.Items["__ModelStateErrors"] = errors;

            // Let MVC short-circuit with 400; middleware will shape body if response not started.
            context.Result = new BadRequestResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
