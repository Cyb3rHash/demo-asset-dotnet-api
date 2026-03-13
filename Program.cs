using System.Text.Json;
using DemoAssetDotnetApi.Api.Middleware;
using DemoAssetDotnetApi.Api.OpenApi;
using DemoAssetDotnetApi.Api.Validation;
using DemoAssetDotnetApi.Application.Assets;
using DemoAssetDotnetApi.Domain.Errors;
using DemoAssetDotnetApi.Infrastructure.Persistence;
using FluentValidation;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------
// Kestrel listeners (HTTP + optional HTTPS)
// -------------------------------
//
// Some preview/ingress setups connect to the container over HTTPS (upstream HTTPS) even though
// TLS is also terminated at the edge. If the container only listens on HTTP, nginx can return 502.
// To be robust, we:
// - always enable HTTP on the expected PORT
// - additionally enable HTTPS on the same PORT *when a dev cert is available*
// - never fail startup if HTTPS cert is missing (fallback to HTTP-only)
//
// NOTE: For production, a real certificate should be provisioned via environment/secret mounting.
var portStr = Environment.GetEnvironmentVariable("PORT");
var listenPort = 3010;
if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out var parsedPort))
{
    listenPort = parsedPort;
}

builder.WebHost.ConfigureKestrel(options =>
{
    // Always listen on HTTP so local curl/health checks and HTTP-based proxies work.
    options.ListenAnyIP(listenPort);

    // Best-effort HTTPS: if no cert is available, we simply won't add the HTTPS endpoint.
    try
    {
        options.ListenAnyIP(listenPort, listen => listen.UseHttps());
    }
    catch
    {
        // Swallow: in many container environments the dev cert is not present.
        // HTTP will remain available on the configured port.
    }
});

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

// Health checks (ASP.NET Core standard).
// Used by preview/proxy environments to determine container readiness/liveness.
builder.Services.AddHealthChecks();

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
//
// Register validators into DI. (MVC auto-validation is intentionally not wired here because the
// AddFluentValidation* MVC extension methods are not available with the current package set.)
builder.Services.AddValidatorsFromAssemblyContaining<ManageSiteAssetsRequestValidator>();

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

﻿// -------------------------------
// Host/port binding (container-friendly)
// -------------------------------
//
// NOTE: We configure Kestrel listeners explicitly above (HTTP + optional HTTPS) using PORT.
// Therefore, we intentionally avoid mutating app.Urls here to prevent conflicts and ambiguity
// when the host also sets ASPNETCORE_URLS.

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

// Reverse proxy / ingress support.
// Many preview environments terminate TLS at the proxy and forward requests over HTTP to Kestrel.
// Without forwarded headers, ASP.NET Core may mis-detect scheme/host which can break redirects and URL generation.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};

// In preview/CI environments we don't know proxy IPs ahead of time; clear defaults to accept forwarded headers.
// NOTE: This should be tightened for production deployments where proxy networks are known.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

// Do NOT redirect HTTP->HTTPS inside the container by default.
// The proxy/ingress typically handles HTTPS externally. Enforcing HTTPS here can cause redirect loops or bad gateway behavior.
if (app.Environment.IsDevelopment())
{
    // Intentionally no HTTPS redirection in development/preview.
    // app.UseHttpsRedirection();
}

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

// Root endpoint: some ingress/proxy setups probe `/` by default.
// Return 200 to avoid upstream being marked unhealthy (which can surface as 502).
app.MapGet("/", () => Results.Ok(new
    {
        service = "demo-asset-dotnet-api",
        status = "ok",
        healthz = "/healthz",
        swagger = "/swagger/index.html"
    }))
    .WithName("Root")
    .WithSummary("Root endpoint (proxy-friendly)")
    .WithDescription("Returns a simple 200 OK payload. Useful for preview/ingress health probing and quick verification.")
    .WithTags("System")
    .WithOpenApi();

// Controllers
app.MapControllers();

// Health check endpoint used by preview/proxy health probing.
// Returns 200 when the app is running and can serve requests.
app.MapHealthChecks("/healthz", new HealthCheckOptions
    {
        // No custom checks registered; this will report Healthy when the app is up.
        // If checks are added later, this predicate ensures they are included by default.
        Predicate = _ => true,
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    })
    .WithName("Healthz")
    .WithSummary("Health check (proxy-ready)")
    .WithDescription("ASP.NET Core health checks endpoint used for container readiness/liveness probes.")
    .WithTags("System")
    .WithOpenApi();

// Minimal health check endpoint (kept).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck")
    .WithSummary("Health check")
    .WithDescription("Simple health endpoint for monitoring and container readiness/liveness checks.")
    .WithTags("System")
    .WithOpenApi();

app.Run();
