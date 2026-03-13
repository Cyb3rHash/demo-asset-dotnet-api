using DemoAssetDotnetApi.Api.LinkedModules;
using DemoAssetDotnetApi.Api.Middleware;
using DemoAssetDotnetApi.Api.Validation;
using DemoAssetDotnetApi.Infrastructure.Persistence;

namespace DemoAssetDotnetApi.Application.LinkedModules;

/// <summary>
/// Canonical application flow for linked modules (EF Source Mapping, Calculated Throughput, Data Input).
/// </summary>
public sealed class LinkedModuleService : ILinkedModuleService
{
    private readonly ILinkedModuleRepository _repo;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LinkedModuleService> _logger;

    public LinkedModuleService(ILinkedModuleRepository repo, IHttpContextAccessor httpContextAccessor, ILogger<LinkedModuleService> logger)
    {
        _repo = repo;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<GetEfSourceMappingsForSiteResponse> GetEfMappingsAsync(GetEfSourceMappingsForSiteRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation("siteId is required.", "REQUIRED", "EFSourceMapping", "siteId");
        }

        var mappings = await _repo.ListEfMappingsAsync(request.SiteId, request.AssetId, ct);

        return new GetEfSourceMappingsForSiteResponse
        {
            CorrelationId = correlationId,
            SiteId = request.SiteId,
            Mappings = mappings
        };
    }

    public async Task<ManageEfSourceMappingsResponse> UpsertEfMappingsAsync(ManageEfSourceMappingsRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation("siteId is required.", "REQUIRED", "EFSourceMapping", "siteId");
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            throw ValidationExtensions.DomainValidation("assetId is required.", "REQUIRED", "EFSourceMapping", "assetId");
        }

        await _repo.UpsertEfMappingsAsync(request.SiteId, request.AssetId, request.Mappings, ct);

        var saved = await _repo.ListEfMappingsAsync(request.SiteId, request.AssetId, ct);

        return new ManageEfSourceMappingsResponse
        {
            CorrelationId = correlationId,
            SiteId = request.SiteId,
            AssetId = request.AssetId,
            Mappings = saved
        };
    }

    public async Task<GetCalculatedThroughputInputsForSiteResponse> GetThroughputEquationsAsync(GetCalculatedThroughputInputsForSiteRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation("siteId is required.", "REQUIRED", "ThroughputSetup", "siteId");
        }

        if (request.ReportingYear <= 0)
        {
            throw ValidationExtensions.DomainValidation("reportingYear is required.", "REQUIRED", "ThroughputSetup", "reportingYear");
        }

        var eqs = await _repo.ListThroughputEquationsAsync(request.SiteId, request.AssetId, request.ReportingYear, ct);

        return new GetCalculatedThroughputInputsForSiteResponse
        {
            CorrelationId = correlationId,
            SiteId = request.SiteId,
            ReportingYear = request.ReportingYear,
            Equations = eqs
        };
    }

    public async Task<ManageCalculatedThroughputInputsResponse> UpsertThroughputEquationsAsync(ManageCalculatedThroughputInputsRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation("siteId is required.", "REQUIRED", "ThroughputSetup", "siteId");
        }

        if (request.ReportingYear <= 0)
        {
            throw ValidationExtensions.DomainValidation("reportingYear is required.", "REQUIRED", "ThroughputSetup", "reportingYear");
        }

        // Persist. Note: request equations may contain mixed assets; repository stores by assetId+year.
        await _repo.UpsertThroughputEquationsBulkAsync(request.SiteId, request.ReportingYear, request.Equations, ct);

        var siteEqs = await _repo.ListThroughputEquationsAsync(request.SiteId, assetId: null, request.ReportingYear, ct);

        return new ManageCalculatedThroughputInputsResponse
        {
            CorrelationId = correlationId,
            SiteId = request.SiteId,
            ReportingYear = request.ReportingYear,
            Equations = siteEqs
        };
    }

    public async Task<GenerateThroughputForInputParameterResponse> GenerateThroughputAsync(GenerateThroughputForInputParameterRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation("siteId is required.", "REQUIRED", "ThroughputSetup", "siteId");
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            throw ValidationExtensions.DomainValidation("assetId is required.", "REQUIRED", "ThroughputSetup", "assetId");
        }

        if (string.IsNullOrWhiteSpace(request.InputParameterId))
        {
            throw ValidationExtensions.DomainValidation("inputParameterId is required.", "REQUIRED", "ThroughputSetup", "inputParameterId");
        }

        if (request.ReportingYear <= 0)
        {
            throw ValidationExtensions.DomainValidation("reportingYear is required.", "REQUIRED", "ThroughputSetup", "reportingYear");
        }

        var eqs = await _repo.ListThroughputEquationsAsync(request.SiteId, request.AssetId, request.ReportingYear, ct);
        var eq = eqs.FirstOrDefault(e => string.Equals(e.InputParameterId, request.InputParameterId, StringComparison.OrdinalIgnoreCase));
        if (eq is null)
        {
            // For POC we treat as conflict (missing setup).
            throw new DemoAssetDotnetApi.Domain.Errors.DomainConflictException("No throughput equation setup found for inputParameterId in given year.");
        }

        // Deterministic POC value:
        // base = number of scalars + length of masterEquationId modulo small range; this is stable and debuggable.
        var scalarSum = eq.Scalars.Sum(s => s.ScalarValue ?? 0m);
        var baseFactor = (eq.MasterEquationId.Length % 7) + 1;
        var value = (scalarSum == 0m ? baseFactor : scalarSum) * 1.0m;

        _logger.LogInformation(
            "Throughput generated (POC). siteId={SiteId} assetId={AssetId} year={Year} inputParameterId={InputParameterId} value={Value}",
            request.SiteId,
            request.AssetId,
            request.ReportingYear,
            request.InputParameterId,
            value);

        return new GenerateThroughputForInputParameterResponse
        {
            CorrelationId = correlationId,
            SiteId = request.SiteId,
            AssetId = request.AssetId,
            ReportingYear = request.ReportingYear,
            InputParameterId = request.InputParameterId,
            ThroughputValue = value,
            ValueBasis = "Stub"
        };
    }

    public async Task<UpsertDataInputValueResponse> UpsertDataInputValueAsync(UpsertDataInputValueRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation("siteId is required.", "REQUIRED", "DataInput", "siteId");
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            throw ValidationExtensions.DomainValidation("assetId is required.", "REQUIRED", "DataInput", "assetId");
        }

        if (string.IsNullOrWhiteSpace(request.InputParameterId))
        {
            throw ValidationExtensions.DomainValidation("inputParameterId is required.", "REQUIRED", "DataInput", "inputParameterId");
        }

        if (request.ReportingYear <= 0)
        {
            throw ValidationExtensions.DomainValidation("reportingYear is required.", "REQUIRED", "DataInput", "reportingYear");
        }

        await _repo.UpsertDataInputValueAsync(request.SiteId, request, ct);

        return new UpsertDataInputValueResponse
        {
            CorrelationId = correlationId,
            SiteId = request.SiteId,
            AssetId = request.AssetId,
            ReportingYear = request.ReportingYear,
            InputParameterId = request.InputParameterId,
            Value = request.Value
        };
    }

    private string GetCorrelationId()
    {
        var ctx = _httpContextAccessor.HttpContext;
        return ctx?.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) == true
            ? v?.ToString() ?? string.Empty
            : string.Empty;
    }
}
