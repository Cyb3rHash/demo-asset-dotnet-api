namespace DemoAssetDotnetApi.Domain.Errors;

/// <summary>
/// Validation exception that should map to HTTP 400 with error envelope.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public IReadOnlyList<ValidationErrorDetail>? Errors { get; }

    public DomainValidationException(string message, IReadOnlyList<ValidationErrorDetail>? errors = null) : base(message)
    {
        Errors = errors;
    }
}

/// <summary>
/// Business conflict exception that should map to HTTP 409 with error envelope.
/// </summary>
public sealed class DomainConflictException : Exception
{
    public IReadOnlyList<ValidationErrorDetail>? Errors { get; }

    public DomainConflictException(string message, IReadOnlyList<ValidationErrorDetail>? errors = null) : base(message)
    {
        Errors = errors;
    }
}
