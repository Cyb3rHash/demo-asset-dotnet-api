namespace DemoAssetDotnetApi.Domain.Errors;

/// <summary>
/// Standard error envelope aligned to CodeWiki specs.
/// </summary>
public sealed class ErrorResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Category { get; set; } = ErrorCategory.System.ToString();
    public string Message { get; set; } = "An error occurred.";
    public List<ValidationErrorDetail>? Errors { get; set; }
}
