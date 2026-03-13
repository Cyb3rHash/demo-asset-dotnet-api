using DemoAssetDotnetApi.Api.Middleware;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DemoAssetDotnetApi.Api.OpenApi;

/// <summary>
/// Adds the X-Correlation-Id header to all operations in Swagger.
/// </summary>
public sealed class CorrelationIdHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        // REQ: REQ-002 - Correlation header semantics documented in OpenAPI.
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = CorrelationIdMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional correlation ID for tracing. If omitted, server generates one and returns it in the same header.",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
