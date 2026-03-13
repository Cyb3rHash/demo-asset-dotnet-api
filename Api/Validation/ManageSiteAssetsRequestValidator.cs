using DemoAssetDotnetApi.Api.SiteAssets;
using FluentValidation;

namespace DemoAssetDotnetApi.Api.Validation;

/// <summary>
/// Validator for ManageSiteAssetsRequest (BRD-aligned aggregate save).
/// </summary>
public sealed class ManageSiteAssetsRequestValidator : AbstractValidator<ManageSiteAssetsRequest>
{
    public ManageSiteAssetsRequestValidator()
    {
        // REQ: REQ-004 - Validation failures map to 400 Validation with field-level details (tab + fieldPath).

        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("REQUIRED")
            .WithMessage("siteId is required.")
            .WithTabAndFieldPath("AssetDetails", "siteId");

        RuleFor(x => x.Asset)
            .NotNull()
            .WithErrorCode("REQUIRED")
            .WithMessage("asset is required.")
            .WithTabAndFieldPath("AssetDetails", "asset");

        RuleFor(x => x.Asset.AssetName)
            .NotEmpty()
            .WithErrorCode("REQUIRED")
            .WithMessage("Asset Name is required.")
            .WithTabAndFieldPath("AssetDetails", "asset.assetName");

        RuleFor(x => x.Asset.PermitEuId)
            .NotEmpty()
            .WithErrorCode("REQUIRED")
            .WithMessage("Permit EU ID is required.")
            .WithTabAndFieldPath("AssetDetails", "asset.permitEuId");

        RuleFor(x => x.Asset.GlobalUniqueAssetId)
            .NotEmpty()
            .WithErrorCode("REQUIRED")
            .WithMessage("Global Unique Asset ID is required.")
            .WithTabAndFieldPath("AssetDetails", "asset.globalUniqueAssetId");

        When(x => x.Operation == ManageSiteAssetsOperation.Edit, () =>
        {
            RuleFor(x => x.Asset.AssetId)
                .NotEmpty()
                .WithErrorCode("REQUIRED")
                .WithMessage("assetId is required for Edit.")
                .WithTabAndFieldPath("AssetDetails", "asset.assetId");
        });

        When(x => x.Operation == ManageSiteAssetsOperation.Copy, () =>
        {
            // Copy requires CopyMetadata and a sourceAssetId.
            RuleFor(x => x.CopyMetadata)
                .NotNull()
                .WithErrorCode("REQUIRED")
                .WithMessage("copyMetadata is required for Copy.")
                .WithTabAndFieldPath("AssetDetails", "copyMetadata");

            RuleFor(x => x.CopyMetadata!.SourceAssetId)
                .NotEmpty()
                .WithErrorCode("REQUIRED")
                .WithMessage("copyMetadata.sourceAssetId is required for Copy.")
                .WithTabAndFieldPath("AssetDetails", "copyMetadata.sourceAssetId");

            // Recommended idempotency key for safe retries.
            RuleFor(x => x.IdempotencyKey)
                .NotEmpty()
                .WithErrorCode("RECOMMENDED")
                .WithMessage("idempotencyKey is recommended for Copy operations to support safe retries.")
                .WithTabAndFieldPath("AssetDetails", "idempotencyKey");
        });
    }
}
