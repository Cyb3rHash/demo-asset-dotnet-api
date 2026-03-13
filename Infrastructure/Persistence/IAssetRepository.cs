using DemoAssetDotnetApi.Api.SiteAssets;

namespace DemoAssetDotnetApi.Infrastructure.Persistence;

public interface IAssetRepository
{
    Task<AssetAggregateDto> UpsertAggregateAsync(
        string siteId,
        AssetAggregateDto asset,
        ManageSiteAssetsOperation operation,
        string? idempotencyKey,
        CopyMetadataDto? copyMetadata,
        CancellationToken ct);

    Task<List<AssetListItemDto>> ListAsync(string siteId, string? search, bool includeDeleted, CancellationToken ct);

    Task<AssetAggregateDto?> GetByIdAsync(string siteId, string assetId, CancellationToken ct);

    Task<bool> SoftDeleteAsync(string siteId, string assetId, string? reason, CancellationToken ct);

    Task<bool> HardDeleteAsync(string siteId, string assetId, string? reason, CancellationToken ct);

    Task<CopyLineageDto?> GetCopyLineageForTargetAsync(string siteId, string targetAssetId, CancellationToken ct);

    Task UpdateCopyLineageReplicationAsync(string siteId, CopyLineageDto lineage, CancellationToken ct);
}
