namespace DemoAssetDotnetApi.Api.Middleware;

/// <summary>
/// Adds a structured logging scope for each request.
/// This avoids one-off logging patterns and keeps correlation-based debugging consistent.
/// </summary>
public sealed class RequestLoggingScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingScopeMiddleware> _logger;

    public RequestLoggingScopeMiddleware(RequestDelegate next, ILogger<RequestLoggingScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // REQ: REQ-005 - Structured logging with correlation context.
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v?.ToString() : null;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = correlationId,
            ["httpMethod"] = context.Request.Method,
            ["httpPath"] = context.Request.Path.Value,
        }))
        {
            await _next(context);
        }
    }
}
