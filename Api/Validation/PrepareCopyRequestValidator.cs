using DemoAssetDotnetApi.Api.SiteAssets;
using FluentValidation;

namespace DemoAssetDotnetApi.Api.Validation;

/// <summary>
/// Validator for PrepareCopyRequest (BRD-aligned Copy confirmation hydration).
/// </summary>
public sealed class PrepareCopyRequestValidator : AbstractValidator<PrepareCopyRequest>
{
    public PrepareCopyRequestValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("REQUIRED")
            .WithMessage("siteId is required.")
            .WithTabAndFieldPath("AssetDetails", "siteId");

        RuleFor(x => x.SourceAssetId)
            .NotEmpty()
            .WithErrorCode("REQUIRED")
            .WithMessage("sourceAssetId is required.")
            .WithTabAndFieldPath("AssetDetails", "sourceAssetId");
    }
}
