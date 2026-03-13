using DemoAssetDotnetApi.Api.SiteAssets;

namespace DemoAssetDotnetApi.Application.Assets;

public interface IAssetService
{
    Task<ManageSiteAssetsResponse> ManageAsync(ManageSiteAssetsRequest request, CancellationToken ct);
    Task<GetSiteAssetsResponse> ListAsync(string siteId, string? search, bool includeDeleted, CancellationToken ct);
    Task<GetSiteAssetByIdResponse> GetByIdAsync(string siteId, string assetId, CancellationToken ct);
    Task<RemoveSiteAssetResponse> RemoveAsync(RemoveSiteAssetRequest request, CancellationToken ct);

    Task<PrepareCopyResponse> PrepareCopyAsync(PrepareCopyRequest request, CancellationToken ct);
}
