using System.Text.Json;
using DemoAssetDotnetApi.Api.Middleware;
using DemoAssetDotnetApi.Api.OpenApi;
using DemoAssetDotnetApi.Api.Validation;
using DemoAssetDotnetApi.Application.Assets;
using DemoAssetDotnetApi.Domain.Errors;
using DemoAssetDotnetApi.Infrastructure.Persistence;
using FluentValidation;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------
// Services
// -------------------------------

// REQ: REQ-001 - Layered structure baseline (API/Application/Domain/Infrastructure) with reusable flows.
builder.Services.AddSingleton<IAssetRepository, InMemoryAssetRepository>();

builder.Services.AddSingleton<ILinkedModuleRepository, InMemoryLinkedModuleRepository>();
builder.Services.AddSingleton<DemoAssetDotnetApi.Application.LinkedModules.ILinkedModuleService, DemoAssetDotnetApi.Application.LinkedModules.LinkedModuleService>();

// Replication orchestrator used by Copy save.
builder.Services.AddSingleton<DemoAssetDotnetApi.Application.Replication.ReplicationOrchestrator>();

builder.Services.AddSingleton<IAssetService, AssetService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers(options =>
    {
        // Capture model binding errors for consistent envelope formatting.
        options.Filters.Add<ModelStateEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Keep contract stable and predictable.
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// FluentValidation integration.
// REQ: REQ-004 - Validation pipeline returning standardized error envelope with tab + fieldPath.
builder.Services.AddValidatorsFromAssemblyContaining<ManageSiteAssetsRequestValidator>();

// NOTE: FluentValidation.AspNetCore integrates via MVC when registered.
// Avoid AddFluentValidationAutoValidation() since it is not available with the current package set.
builder.Services.AddFluentValidation(fv =>
{
    fv.DisableDataAnnotationsValidation = true;
    fv.RegisterValidatorsFromAssemblyContaining<ManageSiteAssetsRequestValidator>();
});

// Request body limits (Kestrel + form features).
// REQ: REQ-006 - Request limits / payload size limits as baseline hardening.
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5 MB
});
builder.Services.Configure<RouteHandlerOptions>(_ =>
{
    // Placeholder for future endpoint-level limits if using minimal APIs.
});

// ProblemDetails middleware (used as an exception boundary) - we still return the BRD envelope for API consumers.
// REQ: REQ-003 - Standardized error handling boundary with correlation ID.
builder.Services.AddProblemDetails(options =>
{
    options.IncludeExceptionDetails = (_, _) => builder.Environment.IsDevelopment();

    // Map known domain exceptions to HTTP status codes.
    options.Map<DomainValidationException>((_, ex) =>
    {
        var pd = new ProblemDetails
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message,
            Type = "https://httpstatuses.com/400"
        };
        pd.Extensions["category"] = ErrorCategory.Validation.ToString();
        return pd;
    });

    options.Map<DomainConflictException>((_, ex) =>
    {
        var pd = new ProblemDetails
        {
            Title = "Business conflict",
            Status = StatusCodes.Status409Conflict,
            Detail = ex.Message,
            Type = "https://httpstatuses.com/409"
        };
        pd.Extensions["category"] = ErrorCategory.Business.ToString();
        return pd;
    });
});

// CORS (SPA-friendly)
// REQ: CORS configured for the React SPA
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // For the demo container, allow any origin. Tighten in production by configuring allowed origins.
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Expose correlation ID so browser clients can read it.
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
    });
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Demo Asset .NET API",
        Version = "v1",
        Description =
            "ASP.NET Core (.NET 8) Web API backend baseline aligned to CodeWiki BRD specs: correlation IDs, standardized error envelope, validation, and status-code mappings."
    });

    options.EnableAnnotations();
    options.OperationFilter<CorrelationIdHeaderOperationFilter>();
    options.SchemaFilter<ValidationErrorDetailSchemaFilter>();

    // Standardized error envelope schema for swagger responses.
    options.MapType<ErrorResponse>(() => new OpenApiSchema
    {
        Type = "object",
        Properties =
        {
            ["correlationId"] = new OpenApiSchema { Type = "string", Description = "Correlation ID for debugging and log tracing." },
            ["category"] = new OpenApiSchema { Type = "string", Description = "Validation | Business | System" },
            ["message"] = new OpenApiSchema { Type = "string" },
            ["errors"] = new OpenApiSchema
            {
                Type = "array",
                Items = new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = nameof(ValidationErrorDetail) } }
            }
        }
    });

    // Register example providers (kept local in API layer).
    options.SchemaFilter<SwaggerExamplesSchemaFilter>();
});

var app = builder.Build();

// -------------------------------
// Host/port binding (container-friendly)
// -------------------------------
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// -------------------------------
// Middleware pipeline
// -------------------------------

// REQ: REQ-006 - Request limits / payload size limits.
// Server-wide max request body size (5 MB). Individual endpoints can tighten later.
app.Use(async (ctx, next) =>
{
    var bodySizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (bodySizeFeature is { IsReadOnly: false })
    {
        bodySizeFeature.MaxRequestBodySize = 5 * 1024 * 1024;
    }

    await next();
});

// Correlation ID must be early, so logs and error envelopes always contain it.
// REQ: REQ-002 - Correlation ID extraction/generation and propagation using X-Correlation-Id.
app.UseMiddleware<CorrelationIdMiddleware>();

// Structured logging scope tied to correlation ID.
// REQ: REQ-005 - Structured logging with correlation context.
app.UseMiddleware<RequestLoggingScopeMiddleware>();

// ProblemDetails should be before our envelope mapper; it handles exceptions and sets status codes.
app.UseProblemDetails();

// Convert framework errors + FluentValidation model state into the BRD error envelope.
// REQ: REQ-003 - Standardized error envelope with status code mapping rules.
app.UseMiddleware<ErrorEnvelopeMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Demo Asset .NET API v1");
    options.RoutePrefix = "swagger";
});

// CORS must be in pipeline before endpoints
app.UseCors();

// Controllers
app.MapControllers();

// Minimal health check endpoint (kept).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck")
    .WithSummary("Health check")
    .WithDescription("Simple health endpoint for monitoring and container readiness/liveness checks.")
    .WithTags("System")
    .WithOpenApi();

app.Run();
