using DemoAssetDotnetApi.Api.LinkedModules;
using DemoAssetDotnetApi.Api.Middleware;
using DemoAssetDotnetApi.Api.SiteAssets;
using DemoAssetDotnetApi.Api.Validation;
using DemoAssetDotnetApi.Application.Replication;
using DemoAssetDotnetApi.Domain.Errors;
using DemoAssetDotnetApi.Infrastructure.Persistence;

namespace DemoAssetDotnetApi.Application.Assets;

/// <summary>
/// Application flow for asset operations (Add/Edit/Copy/Delete, list, detail).
/// </summary>
public sealed class AssetService : IAssetService
{
    private readonly IAssetRepository _repo;
    private readonly ReplicationOrchestrator _replication;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AssetService> _logger;

    public AssetService(
        IAssetRepository repo,
        ReplicationOrchestrator replication,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AssetService> logger)
    {
        _repo = repo;
        _replication = replication;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ManageSiteAssetsResponse> ManageAsync(ManageSiteAssetsRequest request, CancellationToken ct)
    {
        // REQ: REQ-001 - Single canonical flow for save behavior.
        var correlationId = GetCorrelationId();

        _logger.LogInformation(
            "AssetService.ManageAsync start operation={Operation} siteId={SiteId} assetId={AssetId}",
            request.Operation,
            request.SiteId,
            request.Asset.AssetId);

        // Additional enforcement beyond FluentValidation (defense-in-depth / explicit errors).
        if (request.Operation == ManageSiteAssetsOperation.Edit && string.IsNullOrWhiteSpace(request.Asset.AssetId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "assetId is required for Edit.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "asset.assetId");
        }

        if (request.Operation == ManageSiteAssetsOperation.Copy)
        {
            if (request.CopyMetadata is null || string.IsNullOrWhiteSpace(request.CopyMetadata.SourceAssetId))
            {
                throw ValidationExtensions.DomainValidation(
                    message: "copyMetadata.sourceAssetId is required for Copy.",
                    code: "REQUIRED",
                    tab: "AssetDetails",
                    fieldPath: "copyMetadata.sourceAssetId");
            }
        }

        // Persist (repository is authoritative for uniqueness and existence checks).
        var saved = await _repo.UpsertAggregateAsync(
            request.SiteId,
            request.Asset,
            request.Operation,
            request.IdempotencyKey,
            request.CopyMetadata,
            ct);

        // Success response.
        var response = new ManageSiteAssetsResponse
        {
            CorrelationId = correlationId,
            Asset = saved
        };

        if (request.Operation == ManageSiteAssetsOperation.Copy && request.CopyMetadata is not null)
        {
            // REQ: BRD §6.13 - Copy and lineage capture must include replication outcome status + impacted module list.
            // Prefer server-authoritative lineage (repository stores it for idempotency correctness).
            var lineage = await _repo.GetCopyLineageForTargetAsync(request.SiteId, saved.AssetId ?? string.Empty, ct);
            response.CopyLineage = lineage ?? new CopyLineageDto
            {
                CopyOperationId = string.IsNullOrWhiteSpace(request.CopyMetadata.CopyOperationId)
                    ? Guid.NewGuid().ToString("D")
                    : request.CopyMetadata.CopyOperationId,
                SourceAssetId = request.CopyMetadata.SourceAssetId,
                TargetAssetId = saved.AssetId ?? string.Empty,
                TimestampUtc = string.IsNullOrWhiteSpace(request.CopyMetadata.CopyTimestampUtc)
                    ? DateTime.UtcNow.ToString("O")
                    : request.CopyMetadata.CopyTimestampUtc,
                PerformedBy = request.CopyMetadata.CopyPerformedBy,
                ReplicationResultStatus = "Failed",
                ImpactedModules = new List<string> { LinkedModuleContracts.Modules.EfSourceMapping, LinkedModuleContracts.Modules.CalculatedThroughputSetup, LinkedModuleContracts.Modules.DataInput },
                ReasonCode = "LINEAGE_NOT_FOUND"
            };

            // Invoke replication orchestrator AFTER save (Copy).
            // REQ: FR-03 - Save must proceed save + replication; return transparent outcome.
            var outcome = await _replication.ReplicateAfterCopyAsync(request.SiteId, response.CopyLineage, ct);

            // Persist outcome into lineage and return summary.
            response.CopyLineage.ReplicationResultStatus = outcome.Status.ToString();
            response.CopyLineage.ImpactedModules = outcome.ImpactedModules;
            response.CopyLineage.ReasonCode = outcome.ReasonCode;

            await _repo.UpdateCopyLineageReplicationAsync(request.SiteId, response.CopyLineage, ct);

            response.Replication = new ReplicationSummaryDto
            {
                Status = outcome.Status.ToString(),
                ImpactedModules = outcome.ImpactedModules,
                Details = outcome.Details.Select(d => new ReplicationDetailDto
                {
                    Module = d.Module,
                    Status = d.Status.ToString(),
                    Message = d.Message
                }).ToList()
            };
        }

        _logger.LogInformation(
            "AssetService.ManageAsync success siteId={SiteId} assetId={AssetId}",
            request.SiteId,
            response.Asset.AssetId);

        return response;
    }

    public async Task<GetSiteAssetsResponse> ListAsync(string siteId, string? search, bool includeDeleted, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "siteId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "siteId");
        }

        var assets = await _repo.ListAsync(siteId, search, includeDeleted, ct);

        return new GetSiteAssetsResponse
        {
            CorrelationId = correlationId,
            SiteId = siteId,
            Assets = assets
        };
    }

    public async Task<GetSiteAssetByIdResponse> GetByIdAsync(string siteId, string assetId, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "siteId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "siteId");
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "assetId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "assetId");
        }

        var asset = await _repo.GetByIdAsync(siteId, assetId, ct);
        if (asset is null)
        {
            // Treat not-found as Business conflict for compatibility with existing taxonomy usage (409).
            throw new DomainConflictException($"Asset '{assetId}' not found for site '{siteId}'.");
        }

        return new GetSiteAssetByIdResponse
        {
            CorrelationId = correlationId,
            SiteId = siteId,
            Asset = asset
        };
    }

    public async Task<RemoveSiteAssetResponse> RemoveAsync(RemoveSiteAssetRequest request, CancellationToken ct)
    {
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "siteId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "siteId");
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "assetId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "assetId");
        }

        if (!request.Confirm)
        {
            throw ValidationExtensions.DomainValidation(
                message: "confirm=true is required to delete an asset.",
                code: "CONFIRM_REQUIRED",
                tab: "AssetDetails",
                fieldPath: "confirm");
        }

        var mode = request.DeleteMode ?? SiteAssets.DeleteMode.Soft;

        _logger.LogInformation(
            "AssetService.RemoveAsync start siteId={SiteId} assetId={AssetId} mode={Mode}",
            request.SiteId,
            request.AssetId,
            mode);

        bool deleted;
        if (mode == SiteAssets.DeleteMode.Hard)
        {
            deleted = await _repo.HardDeleteAsync(request.SiteId, request.AssetId, request.Reason, ct);
        }
        else
        {
            deleted = await _repo.SoftDeleteAsync(request.SiteId, request.AssetId, request.Reason, ct);
        }

        if (!deleted)
        {
            throw new DomainConflictException($"Asset '{request.AssetId}' not found for site '{request.SiteId}'.");
        }

        return new RemoveSiteAssetResponse
        {
            CorrelationId = correlationId,
            Result = new RemoveSiteAssetResultDto
            {
                Status = "Deleted",
                Message = mode == SiteAssets.DeleteMode.Hard ? "Asset hard deleted." : "Asset deleted (soft).",
                BlockedBy = new List<DeleteBlockedByDto>()
            }
        };
    }

    public async Task<PrepareCopyResponse> PrepareCopyAsync(PrepareCopyRequest request, CancellationToken ct)
    {
        // REQ: FR-03 (CodeWiki build spec) - Provide pre-save confirmation payload and hydrated source snapshot.
        var correlationId = GetCorrelationId();

        if (string.IsNullOrWhiteSpace(request.SiteId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "siteId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "siteId");
        }

        if (string.IsNullOrWhiteSpace(request.SourceAssetId))
        {
            throw ValidationExtensions.DomainValidation(
                message: "sourceAssetId is required.",
                code: "REQUIRED",
                tab: "AssetDetails",
                fieldPath: "sourceAssetId");
        }

        _logger.LogInformation(
            "AssetService.PrepareCopyAsync start siteId={SiteId} sourceAssetId={SourceAssetId} reportingYear={ReportingYear}",
            request.SiteId,
            request.SourceAssetId,
            request.ReportingYear);

        var source = await _repo.GetByIdAsync(request.SiteId, request.SourceAssetId, ct);
        if (source is null)
        {
            throw new DomainConflictException($"Asset '{request.SourceAssetId}' not found for site '{request.SiteId}'.");
        }

        // BRD confirmation fields: Asset Name, Permit EU ID, Status Date.
        // StatusDate is derived as the max FromDate (best-effort) for the POC.
        var statusDate = source.StatusLog
            .Select(s => s.FromDate)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? string.Empty;

        var response = new PrepareCopyResponse
        {
            CorrelationId = correlationId,
            Confirmation = new CopyConfirmationDto
            {
                AssetName = source.AssetName ?? string.Empty,
                PermitEuId = source.PermitEuId ?? string.Empty,
                StatusDate = statusDate
            },
            HydratedSource = new HydratedSourceSnapshotDto
            {
                Asset = source,
                EfSourceMappings = new List<object>(),
                ThroughputSetup = new List<object>()
            }
        };

        _logger.LogInformation(
            "AssetService.PrepareCopyAsync success siteId={SiteId} sourceAssetId={SourceAssetId}",
            request.SiteId,
            request.SourceAssetId);

        return response;
    }

    private string GetCorrelationId()
    {
        var ctx = _httpContextAccessor.HttpContext;
        return ctx?.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) == true
            ? v?.ToString() ?? string.Empty
            : string.Empty;
    }
}
