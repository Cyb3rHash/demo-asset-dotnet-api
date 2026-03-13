using System.Collections.Concurrent;
using DemoAssetDotnetApi.Api.SiteAssets;
using DemoAssetDotnetApi.Domain.Errors;

namespace DemoAssetDotnetApi.Infrastructure.Persistence;

/// <summary>
/// In-memory repository for demo purposes.
/// Stores AssetAggregateDto and supports list/detail/delete operations.
/// </summary>
public sealed class InMemoryAssetRepository : IAssetRepository
{
    private sealed class StoredAsset
    {
        public AssetAggregateDto Asset { get; set; } = new();
        public bool IsDeleted { get; set; }
        public string? DeletedReason { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }

        // Copy lineage (only set when this asset was created via Copy).
        public CopyLineageDto? CopyLineage { get; set; }
    }

    // siteId -> assetId -> stored
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, StoredAsset>> _bySite = new();

    // idempotencyKey -> assetId (Copy only, site-scoped by usage: callers should provide unique keys per logical copy).
    private readonly ConcurrentDictionary<string, string> _idempotencyCopyKeyToAssetId = new();

    public Task<AssetAggregateDto> UpsertAggregateAsync(
        string siteId,
        AssetAggregateDto asset,
        ManageSiteAssetsOperation operation,
        string? idempotencyKey,
        CopyMetadataDto? copyMetadata,
        CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());

        // Copy idempotency: if key already seen, return the previously created asset (server must not duplicate).
        // REQ: Idempotency (Copy) - CodeWiki build spec "Idempotency (Copy)".
        if (operation == ManageSiteAssetsOperation.Copy && !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (_idempotencyCopyKeyToAssetId.TryGetValue(idempotencyKey, out var existingId) &&
                site.TryGetValue(existingId, out var existingStored) &&
                !existingStored.IsDeleted)
            {
                return Task.FromResult(CloneAggregate(existingStored.Asset, siteId));
            }
        }

        if (operation is ManageSiteAssetsOperation.Add or ManageSiteAssetsOperation.Copy)
        {
            // Uniqueness: Permit EU ID must be unique within site among non-deleted assets.
            if (!string.IsNullOrWhiteSpace(asset.PermitEuId) &&
                site.Values.Any(s =>
                    !s.IsDeleted &&
                    string.Equals(s.Asset.PermitEuId, asset.PermitEuId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainConflictException($"Permit EU ID '{asset.PermitEuId}' already exists for site '{siteId}'.");
            }

            var created = CloneAggregate(asset, siteId);

            // Create semantics: always generate new AssetId for Add/Copy.
            created.AssetId = Guid.NewGuid().ToString("D");
            created.SiteId = siteId;

            // Copy create-semantics: new IDs everywhere for persisted rows.
            // REQ: FR-03 Copy create semantics - never reuse source identities; lineage must be explicit.
            if (operation == ManageSiteAssetsOperation.Copy)
            {
                foreach (var s in created.StatusLog) s.StatusLogId = Guid.NewGuid().ToString("D");
                foreach (var a in created.AdditionalAssetIds) a.AdditionalAssetIdId = Guid.NewGuid().ToString("D");
                foreach (var p in created.AssetProperties) p.AssetPropertyId = Guid.NewGuid().ToString("D");
                foreach (var c in created.ControlDevices) c.ControlDeviceMappingId = Guid.NewGuid().ToString("D");
                foreach (var i in created.InputParameters) i.InputParameterId = Guid.NewGuid().ToString("D");
                foreach (var r in created.ReportingAttributes) r.ReportingAttributeId = Guid.NewGuid().ToString("D");
            }

            CopyLineageDto? lineage = null;
            if (operation == ManageSiteAssetsOperation.Copy && copyMetadata is not null)
            {
                lineage = new CopyLineageDto
                {
                    CopyOperationId = copyMetadata.CopyOperationId ?? Guid.NewGuid().ToString("D"),
                    SourceAssetId = copyMetadata.SourceAssetId,
                    TargetAssetId = created.AssetId,
                    TimestampUtc = copyMetadata.CopyTimestampUtc ?? DateTime.UtcNow.ToString("O"),
                    PerformedBy = copyMetadata.CopyPerformedBy,
                    ReplicationResultStatus = "NotApplicable",
                    ImpactedModules = new List<string>(),
                    ReasonCode = null
                };
            }

            site[created.AssetId] = new StoredAsset
            {
                Asset = CloneAggregate(created, siteId),
                IsDeleted = false,
                CopyLineage = lineage
            };

            if (operation == ManageSiteAssetsOperation.Copy && !string.IsNullOrWhiteSpace(idempotencyKey))
            {
                _idempotencyCopyKeyToAssetId[idempotencyKey] = created.AssetId;
            }

            return Task.FromResult(CloneAggregate(created, siteId));
        }

        // Edit
        if (string.IsNullOrWhiteSpace(asset.AssetId))
        {
            throw new DomainValidationException("assetId is required for Edit.");
        }

        if (!site.TryGetValue(asset.AssetId, out var stored) || stored.IsDeleted)
        {
            throw new DomainConflictException($"Asset '{asset.AssetId}' not found for site '{siteId}'.");
        }

        // Uniqueness check for edits (non-deleted assets).
        if (!string.IsNullOrWhiteSpace(asset.PermitEuId) &&
            site.Values.Any(s =>
                !s.IsDeleted &&
                s.Asset.AssetId != asset.AssetId &&
                string.Equals(s.Asset.PermitEuId, asset.PermitEuId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainConflictException($"Permit EU ID '{asset.PermitEuId}' already exists for site '{siteId}'.");
        }

        var updated = CloneAggregate(asset, siteId);
        updated.SiteId = siteId;

        stored.Asset = CloneAggregate(updated, siteId);

        return Task.FromResult(CloneAggregate(updated, siteId));
    }

    public Task<List<AssetListItemDto>> ListAsync(string siteId, string? search, bool includeDeleted, CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());

        var query = site.Values.AsEnumerable();

        if (!includeDeleted)
        {
            query = query.Where(s => !s.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                (s.Asset.AssetName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (s.Asset.PermitEuId ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (s.Asset.GlobalUniqueAssetId ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = query
            .OrderBy(s => s.Asset.AssetName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(s => new AssetListItemDto
            {
                AssetId = s.Asset.AssetId ?? string.Empty,
                AssetName = s.Asset.AssetName ?? string.Empty,
                PermitEuId = s.Asset.PermitEuId ?? string.Empty,
                GlobalUniqueAssetId = s.Asset.GlobalUniqueAssetId,
                IsDeleted = s.IsDeleted
            })
            .ToList();

        return Task.FromResult(list);
    }

    public Task<AssetAggregateDto?> GetByIdAsync(string siteId, string assetId, CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());

        if (!site.TryGetValue(assetId, out var stored) || stored.IsDeleted)
        {
            return Task.FromResult<AssetAggregateDto?>(null);
        }

        return Task.FromResult<AssetAggregateDto?>(CloneAggregate(stored.Asset, siteId));
    }

    public Task<bool> SoftDeleteAsync(string siteId, string assetId, string? reason, CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());

        if (!site.TryGetValue(assetId, out var stored) || stored.IsDeleted)
        {
            return Task.FromResult(false);
        }

        stored.IsDeleted = true;
        stored.DeletedReason = reason;
        stored.DeletedAtUtc = DateTimeOffset.UtcNow;

        return Task.FromResult(true);
    }

    public Task<bool> HardDeleteAsync(string siteId, string assetId, string? reason, CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());
        return Task.FromResult(site.TryRemove(assetId, out _));
    }

    public Task<CopyLineageDto?> GetCopyLineageForTargetAsync(string siteId, string targetAssetId, CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());

        if (!site.TryGetValue(targetAssetId, out var stored) || stored.IsDeleted)
        {
            return Task.FromResult<CopyLineageDto?>(null);
        }

        if (stored.CopyLineage is null)
        {
            return Task.FromResult<CopyLineageDto?>(null);
        }

        return Task.FromResult<CopyLineageDto?>(CloneLineage(stored.CopyLineage));
    }

    public Task UpdateCopyLineageReplicationAsync(string siteId, CopyLineageDto lineage, CancellationToken ct)
    {
        var site = _bySite.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, StoredAsset>());

        if (!site.TryGetValue(lineage.TargetAssetId, out var stored) || stored.IsDeleted)
        {
            // If target not found, no-op (caller should have just saved it; this is defensive for demo).
            return Task.CompletedTask;
        }

        if (stored.CopyLineage is null)
        {
            stored.CopyLineage = CloneLineage(lineage);
            return Task.CompletedTask;
        }

        stored.CopyLineage.ReplicationResultStatus = lineage.ReplicationResultStatus;
        stored.CopyLineage.ImpactedModules = lineage.ImpactedModules.ToList();
        stored.CopyLineage.ReasonCode = lineage.ReasonCode;

        return Task.CompletedTask;
    }

    private static CopyLineageDto CloneLineage(CopyLineageDto src)
    {
        return new CopyLineageDto
        {
            CopyOperationId = src.CopyOperationId,
            SourceAssetId = src.SourceAssetId,
            TargetAssetId = src.TargetAssetId,
            TimestampUtc = src.TimestampUtc,
            PerformedBy = src.PerformedBy,
            ReplicationResultStatus = src.ReplicationResultStatus,
            ImpactedModules = src.ImpactedModules.ToList(),
            ReasonCode = src.ReasonCode
        };
    }

    private static AssetAggregateDto CloneAggregate(AssetAggregateDto src, string siteId)
    {
        // Manual deep-ish clone: keep DTO boundaries and avoid accidental shared references.
        return new AssetAggregateDto
        {
            AssetId = src.AssetId,
            SiteId = siteId,
            AssetGroup = src.AssetGroup,
            ProcessGroup = src.ProcessGroup,
            ProcessGroupOtherText = src.ProcessGroupOtherText,
            AssetName = src.AssetName,
            PermitEuId = src.PermitEuId,
            GlobalUniqueAssetId = src.GlobalUniqueAssetId,
            Description = src.Description,
            StationaryFlag = src.StationaryFlag,
            PseudoChildMapping = src.PseudoChildMapping is null
                ? null
                : new PseudoChildMappingDto
                {
                    Mode = src.PseudoChildMapping.Mode,
                    ParentPseudoAssetId = src.PseudoChildMapping.ParentPseudoAssetId
                },
            StatusLog = src.StatusLog.Select(s => new StatusLogDto
            {
                StatusLogId = s.StatusLogId,
                OperatingStatus = s.OperatingStatus,
                FromDate = s.FromDate,
                ToDate = s.ToDate,
                Comments = s.Comments
            }).ToList(),
            AdditionalAssetIds = src.AdditionalAssetIds.Select(a => new AdditionalAssetIdDto
            {
                AdditionalAssetIdId = a.AdditionalAssetIdId,
                IdType = a.IdType,
                IdValue = a.IdValue,
                InUse = a.InUse
            }).ToList(),
            AssetProperties = src.AssetProperties.Select(p => new AssetPropertyDto
            {
                AssetPropertyId = p.AssetPropertyId,
                PropertyName = p.PropertyName,
                PropertyValue = p.PropertyValue,
                FromDate = p.FromDate,
                Notes = p.Notes,
                InUse = p.InUse
            }).ToList(),
            ControlDevices = src.ControlDevices.Select(c => new ControlDeviceMappingDto
            {
                ControlDeviceMappingId = c.ControlDeviceMappingId,
                ControlDeviceRefId = c.ControlDeviceRefId,
                InUse = c.InUse
            }).ToList(),
            InputParameters = src.InputParameters.Select(i => new InputParameterDto
            {
                InputParameterId = i.InputParameterId,
                Name = i.Name,
                UomId = i.UomId,
                ReportingProgramIds = i.ReportingProgramIds.ToList(),
                InputType = i.InputType,
                DataEntryFrequency = i.DataEntryFrequency,
                FuelMapping = i.FuelMapping is null
                    ? null
                    : new FuelMappingDto { FuelType = i.FuelMapping.FuelType, MappingId = i.FuelMapping.MappingId },
                InUse = i.InUse
            }).ToList(),
            ParentInputMappings = src.ParentInputMappings.Select(m => new ParentInputMappingDto
            {
                ChildInputId = m.ChildInputId,
                ParentInputId = m.ParentInputId
            }).ToList(),
            ReportingAttributes = src.ReportingAttributes.Select(r => new ReportingAttributeDto
            {
                ReportingAttributeId = r.ReportingAttributeId,
                AttributeName = r.AttributeName,
                AttributeValue = r.AttributeValue,
                ReportingProgramId = r.ReportingProgramId,
                InUse = r.InUse
            }).ToList(),
        };
    }
}
