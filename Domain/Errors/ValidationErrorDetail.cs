using DemoAssetDotnetApi.Api.SiteAssets;
using DemoAssetDotnetApi.Api.Validation;
using FluentValidation.Results;

namespace DemoAssetDotnetApi.Domain.Errors;

/// <summary>
/// Field-level validation error detail aligned to CodeWiki specs.
/// </summary>
public sealed class ValidationErrorDetail
{
    public string Code { get; set; } = "VALIDATION_ERROR";
    public string Tab { get; set; } = "Unknown";
    public string FieldPath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";

    public static ValidationErrorDetail FromFluentFailure(ValidationFailure f)
    {
        // REQ: BRD Tab-level validation - Preserve tab + fieldPath for UI routing when provided by validators.
        var tab = "Unknown";
        var fieldPath = f.PropertyName;

        if (f.CustomState is ValidationExtensions.ValidationState st)
        {
            tab = st.Tab;
            fieldPath = st.FieldPath;
        }

        // Validate tab/fieldPath against the contract rules so we never emit malformed routing hints.
        if (!SiteAssetsContracts.AllowedTabs.Contains(tab))
        {
            tab = "Unknown";
        }

        if (!SiteAssetsContracts.IsValidFieldPath(fieldPath))
        {
            fieldPath = f.PropertyName; // best-effort fallback
            if (!SiteAssetsContracts.IsValidFieldPath(fieldPath))
            {
                fieldPath = string.Empty;
            }
        }

        return new ValidationErrorDetail
        {
            Code = string.IsNullOrWhiteSpace(f.ErrorCode) ? "VALIDATION_ERROR" : f.ErrorCode,
            Tab = tab,
            FieldPath = fieldPath,
            Message = f.ErrorMessage,
            Severity = "Error"
        };
    }
}
