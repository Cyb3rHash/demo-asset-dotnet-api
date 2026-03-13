using DemoAssetDotnetApi.Api.SiteAssets;
using DemoAssetDotnetApi.Domain.Errors;
using FluentValidation;

namespace DemoAssetDotnetApi.Api.Validation;

/// <summary>
/// Shared helpers for producing BRD-aligned tab + fieldPath validation errors.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// PUBLIC_INTERFACE
    /// Adds a failure with tab + fieldPath packed into CustomState so the error envelope can map it.
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WithTabAndFieldPath<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        string tab,
        string fieldPath)
    {
        return rule.WithState(_ => new ValidationState
        {
            Tab = tab,
            FieldPath = fieldPath
        });
    }

    /// <summary>
    /// PUBLIC_INTERFACE
    /// Produces a DomainValidationException with fully formed ValidationErrorDetail entries.
    /// Use this for non-FluentValidation checks at the application/service layer.
    /// </summary>
    public static DomainValidationException DomainValidation(string message, string code, string tab, string fieldPath)
    {
        return new DomainValidationException(message, new[]
        {
            new ValidationErrorDetail
            {
                Code = code,
                Tab = SiteAssetsContracts.AllowedTabs.Contains(tab) ? tab : "Unknown",
                FieldPath = SiteAssetsContracts.IsValidFieldPath(fieldPath) ? fieldPath : string.Empty,
                Message = message,
                Severity = "Error"
            }
        });
    }

    public sealed class ValidationState
    {
        public string Tab { get; set; } = "Unknown";
        public string FieldPath { get; set; } = string.Empty;
    }
}
