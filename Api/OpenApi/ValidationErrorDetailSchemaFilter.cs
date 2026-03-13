using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DemoAssetDotnetApi.Api.OpenApi;

/// <summary>
/// Enhances OpenAPI schema for ValidationErrorDetail.
/// </summary>
public sealed class ValidationErrorDetailSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(ValidationErrorDetail))
        {
            return;
        }

        schema.Description = "Field-level validation error with tab and fieldPath for UI routing.";
        schema.Properties["tab"].Description =
            "Tab identifier for UI routing (e.g., AssetDetails, StatusLog, AssetProperties, ControlDevices, InputParameters, ParentInputMapping, ReportingAttributes, EFSourceMapping, ThroughputSetup, DataInput, Unknown).";
        schema.Properties["fieldPath"].Description = "JSONPath-like field path (or dotted path) to highlight the invalid field.";
        schema.Properties["severity"].Description = "Error | Warning";
    }
}
