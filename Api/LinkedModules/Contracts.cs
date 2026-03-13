using System.Text.Json.Serialization;

namespace DemoAssetDotnetApi.Api.LinkedModules;

/// <summary>
/// BRD-observed transport contracts for linked modules:
/// EF Source Mapping, Calculated Throughput Setup, and minimal Data Input.
/// </summary>
public static class LinkedModuleContracts
{
    /// <summary>
    /// Supported replication outcome statuses.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedReplicationStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Partial",
        "Failed",
        "NotApplicable"
    };

    /// <summary>
    /// Canonical module keys used throughout the replication flow.
    /// </summary>
    public static class Modules
    {
        public const string EfSourceMapping = "EFSourceMapping";
        public const string CalculatedThroughputSetup = "CalculatedThroughputSetup";
        public const string DataInput = "DataInput";
    }
}

#region EF Source Mapping

/// <summary>
/// BRD API Inventory:
/// GET api/inputefsourcemapping/getinputefsourcemappingforsite
/// </summary>
public sealed class GetEfSourceMappingsForSiteRequest
{
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Optional filter by assetId (useful for copy verification in POC).
    /// </summary>
    public string? AssetId { get; set; }

    /// <summary>
    /// Optional reporting year context (BRD notes year context for visibility in linked modules).
    /// </summary>
    public int? ReportingYear { get; set; }
}

public sealed class GetEfSourceMappingsForSiteResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public List<EfSourceMappingDto> Mappings { get; set; } = new();
}

/// <summary>
/// Minimal EF mapping record suitable for POC replication demonstration.
/// In a full implementation this would include EF source set/table details and equation/scalars.
/// </summary>
public sealed class EfSourceMappingDto
{
    public string? MappingId { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string InputParameterId { get; set; } = string.Empty;

    public string? EfSourceSet { get; set; }
    public string? EfSourceTable { get; set; }
    public string? EquationExpression { get; set; }

    public List<string> ReportingProgramIds { get; set; } = new();
}

/// <summary>
/// BRD API Inventory:
/// POST api/inputefsourcemapping/manageinputefsourcemapping
/// </summary>
public sealed class ManageEfSourceMappingsRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Upsert list. mappingId optional; server generates if missing.
    /// </summary>
    public List<EfSourceMappingDto> Mappings { get; set; } = new();
}

public sealed class ManageEfSourceMappingsResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public List<EfSourceMappingDto> Mappings { get; set; } = new();
}

#endregion

#region Calculated Throughput Setup

/// <summary>
/// BRD API Inventory:
/// GET api/calculatedthroughputequationsetup/getcalculatedinputparametersforsite
/// </summary>
public sealed class GetCalculatedThroughputInputsForSiteRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string? AssetId { get; set; }
    public int ReportingYear { get; set; }
}

public sealed class GetCalculatedThroughputInputsForSiteResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public List<CalculatedThroughputEquationDto> Equations { get; set; } = new();
}

/// <summary>
/// Minimal throughput equation setup record.
/// </summary>
public sealed class CalculatedThroughputEquationDto
{
    public string? EquationSetupId { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string InputParameterId { get; set; } = string.Empty;

    public string MasterEquationId { get; set; } = string.Empty;
    public string GeneratedEquation { get; set; } = string.Empty;

    public List<EquationScalarDto> Scalars { get; set; } = new();
}

public sealed class EquationScalarDto
{
    public string? ScalarId { get; set; }
    public string ScalarType { get; set; } = string.Empty; // e.g., Factor/Table/Constant
    public string? ScalarRefId { get; set; } // master-data reference if applicable
    public decimal? ScalarValue { get; set; } // value if literal
}

/// <summary>
/// BRD API Inventory:
/// POST api/calculatedthroughputequationsetup/managecalculatedinputparameters
/// </summary>
public sealed class ManageCalculatedThroughputInputsRequest
{
    public string SiteId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public List<CalculatedThroughputEquationDto> Equations { get; set; } = new();
}

public sealed class ManageCalculatedThroughputInputsResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public List<CalculatedThroughputEquationDto> Equations { get; set; } = new();
}

/// <summary>
/// BRD API Inventory:
/// POST api/calculatedthroughputequationsetup/generatethroughputforinputparameter
/// Minimal POC: returns a deterministic computed value (stub) so downstream Data Input can show a value.
/// </summary>
public sealed class GenerateThroughputForInputParameterRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public string InputParameterId { get; set; } = string.Empty;
}

public sealed class GenerateThroughputForInputParameterResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public string InputParameterId { get; set; } = string.Empty;

    public decimal ThroughputValue { get; set; }
    public string ValueBasis { get; set; } = "Stub"; // Stub | Calculated
}

#endregion

#region Data Input (minimal)

/// <summary>
/// Minimal Data Input save used by POC to demonstrate that throughput generation can be persisted as an input value.
/// This is intentionally small; full BRD includes period/frequency handling.
/// </summary>
public sealed class UpsertDataInputValueRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public string InputParameterId { get; set; } = string.Empty;

    /// <summary>
    /// Captured numeric input. In full BRD this varies by frequency/period.
    /// </summary>
    public decimal Value { get; set; }
}

public sealed class UpsertDataInputValueResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public string InputParameterId { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

#endregion

#region Replication outcomes

public enum ReplicationStatus
{
    Completed,
    Partial,
    Failed,
    NotApplicable
}

/// <summary>
/// Replication result for a single module.
/// </summary>
public sealed class ReplicationModuleResultDto
{
    public string Module { get; set; } = string.Empty; // EFSourceMapping | CalculatedThroughputSetup | DataInput
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReplicationStatus Status { get; set; } = ReplicationStatus.NotApplicable;
    public string? Message { get; set; }
}

/// <summary>
/// Persisted replication outcome for a copy operation.
/// </summary>
public sealed class ReplicationOutcomeDto
{
    public string CopyOperationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string SourceAssetId { get; set; } = string.Empty;
    public string TargetAssetId { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReplicationStatus Status { get; set; } = ReplicationStatus.NotApplicable;
    public List<string> ImpactedModules { get; set; } = new();
    public List<ReplicationModuleResultDto> Details { get; set; } = new();
    public string TimestampUtc { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
}

#endregion
