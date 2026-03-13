using Microsoft.Extensions.Primitives;

namespace DemoAssetDotnetApi.Api.Middleware;

/// <summary>
/// Correlation ID middleware.
/// Ensures every request has a correlation ID for traceability and returns it to the caller.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>
    /// The header name used for correlation ID propagation.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// The HttpContext.Items key used to store the correlation ID.
    /// </summary>
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invoke middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // REQ: REQ-002 - Correlation ID extraction/generation and propagation.
        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[ItemKey] = correlationId;

        // Ensure response always includes correlation ID.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues values) &&
            !StringValues.IsNullOrEmpty(values) &&
            !string.IsNullOrWhiteSpace(values[0]))
        {
            return values[0]!;
        }

        var generated = Guid.NewGuid().ToString("D");
        _logger.LogDebug("Generated new correlationId {CorrelationId}", generated);
        return generated;
    }
}
