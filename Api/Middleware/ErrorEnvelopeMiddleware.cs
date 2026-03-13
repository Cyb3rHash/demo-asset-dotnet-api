using System.Net.Mime;
using System.Text.Json;
using DemoAssetDotnetApi.Domain.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace DemoAssetDotnetApi.Api.Middleware;

/// <summary>
/// Converts errors into the BRD-aligned error envelope:
/// { correlationId, category, message, errors[] } with status-code mappings.
/// </summary>
public sealed class ErrorEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorEnvelopeMiddleware> _logger;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public ErrorEnvelopeMiddleware(
        RequestDelegate next,
        ILogger<ErrorEnvelopeMiddleware> logger,
        ProblemDetailsFactory problemDetailsFactory)
    {
        _next = next;
        _logger = logger;
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            // If model binding failed and MVC produced a 400 response, it will be in ModelState,
            // but the response might already be started by MVC.
            if (context.Response.HasStarted)
            {
                return;
            }

            // Convert automatic 400 with ValidationProblemDetails into our envelope.
            // This is a defensive check: controller actions should rely on MVC automatic 400,
            // and this middleware standardizes the output.
            if (context.Response.StatusCode == StatusCodes.Status400BadRequest &&
                context.Items.TryGetValue("__HasModelStateErrors", out var hasErrorsObj) &&
                hasErrorsObj is true)
            {
                // Response body not written yet; we will handle it.
                var validationErrors = context.Items.TryGetValue("__ModelStateErrors", out var errorsObj)
                    ? errorsObj as List<ValidationErrorDetail>
                    : new List<ValidationErrorDetail>();

                await WriteErrorAsync(
                    context,
                    statusCode: StatusCodes.Status400BadRequest,
                    category: ErrorCategory.Validation,
                    message: "Validation failed.",
                    errors: validationErrors);
            }
        }
        catch (DomainValidationException ex)
        {
            await HandleExceptionAsync(context, ex, StatusCodes.Status400BadRequest, ErrorCategory.Validation, ex.Errors);
        }
        catch (DomainConflictException ex)
        {
            await HandleExceptionAsync(context, ex, StatusCodes.Status409Conflict, ErrorCategory.Business, ex.Errors);
        }
        catch (ValidationException ex)
        {
            // FluentValidation exception (rare when auto-validation is enabled, but keep boundary robust).
            var details = ex.Errors.Select(f => ValidationErrorDetail.FromFluentFailure(f)).ToList();
            await HandleExceptionAsync(context, ex, StatusCodes.Status400BadRequest, ErrorCategory.Validation, details);
        }
        catch (Exception ex)
        {
            // REQ: REQ-003 - System failures: HTTP 500 with category=System and user-safe message, log full exception.
            await HandleExceptionAsync(context, ex, StatusCodes.Status500InternalServerError, ErrorCategory.System, errors: null);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception ex,
        int statusCode,
        ErrorCategory category,
        IReadOnlyList<ValidationErrorDetail>? errors)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(ex, "Response already started; cannot write error envelope.");

            // We cannot write our envelope once the response has started; propagate the original exception.
            throw ex;
        }

        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v?.ToString() : null;

        _logger.LogError(
            ex,
            "Request failed with {StatusCode} {Category}. correlationId={CorrelationId}",
            statusCode,
            category,
            correlationId);

        var message = category switch
        {
            ErrorCategory.Validation => ex.Message,
            ErrorCategory.Business => ex.Message,
            _ => "An unexpected error occurred."
        };

        await WriteErrorAsync(context, statusCode, category, message, errors);
    }

    private async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        ErrorCategory category,
        string message,
        IReadOnlyList<ValidationErrorDetail>? errors)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v?.ToString() : null;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var envelope = new ErrorResponse
        {
            CorrelationId = correlationId ?? string.Empty,
            Category = category.ToString(),
            Message = message,
            Errors = errors?.ToList()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, JsonOptions.Default));
    }
}
