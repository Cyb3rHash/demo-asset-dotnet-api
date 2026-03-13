using System.Text.Json.Serialization;

namespace DemoAssetDotnetApi.Api.SiteAssets;

/// <summary>
/// BRD-aligned transport contracts for the SiteAssets API area.
/// </summary>
public static class SiteAssetsContracts
{
    /// <summary>
    /// Allowed tab identifiers for validation routing.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AssetDetails",
        "StatusLog",
        "AssetProperties",
        "ControlDevices",
        "InputParameters",
        "ParentInputMapping",
        "ReportingAttributes",
        "EFSourceMapping",
        "ThroughputSetup",
        "DataInput",
        "Unknown"
    };

    /// <summary>
    /// Validates a dotted/JSONPath-like fieldPath in a conservative way.
    /// We intentionally keep this permissive (letters/digits/_/./[]/-) to match mixed UI payload conventions.
    /// </summary>
    public static bool IsValidFieldPath(string? fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return false;
        }

        foreach (var ch in fieldPath)
        {
            var ok =
                char.IsLetterOrDigit(ch) ||
                ch is '.' or '_' or '[' or ']' or '-' or '$';

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}

public enum ManageSiteAssetsOperation
{
    Add,
    Edit,
    Copy
}

public enum DeleteMode
{
    Soft,
    Hard
}

/// <summary>
/// Save asset aggregate (Add/Edit/Copy).
/// </summary>
public sealed class ManageSiteAssetsRequest
{
    /// <summary>
    /// Operation: Add | Edit | Copy
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ManageSiteAssetsOperation Operation { get; set; } = ManageSiteAssetsOperation.Add;

    /// <summary>
    /// Optional idempotency key (recommended for Copy) to support safe retries.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Site context identifier.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Asset aggregate root (header + tab collections).
    /// </summary>
    public AssetAggregateDto Asset { get; set; } = new();

    /// <summary>
    /// Copy metadata, required when Operation == Copy.
    /// </summary>
    public CopyMetadataDto? CopyMetadata { get; set; }
}

public sealed class CopyMetadataDto
{
    public string SourceAssetId { get; set; } = string.Empty;
    public string? CopyOperationId { get; set; }
    public string? CopyTimestampUtc { get; set; }
    public string? CopyPerformedBy { get; set; }
}

public sealed class ManageSiteAssetsResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public AssetAggregateDto Asset { get; set; } = new();

    /// <summary>
    /// Present for Copy operations; null for Add/Edit.
    /// </summary>
    public CopyLineageDto? CopyLineage { get; set; }

    /// <summary>
    /// Present for Copy operations; null for Add/Edit.
    /// </summary>
    public ReplicationSummaryDto? Replication { get; set; }
}

public sealed class CopyLineageDto
{
    public string CopyOperationId { get; set; } = string.Empty;
    public string SourceAssetId { get; set; } = string.Empty;
    public string TargetAssetId { get; set; } = string.Empty;
    public string TimestampUtc { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public string ReplicationResultStatus { get; set; } = "NotApplicable"; // Completed | Partial | Failed | NotApplicable
    public List<string> ImpactedModules { get; set; } = new(); // EFSourceMapping | CalculatedThroughputSetup | DataInput
    public string? ReasonCode { get; set; }
}

public sealed class ReplicationSummaryDto
{
    public string Status { get; set; } = "NotApplicable"; // NotApplicable | Completed | Partial | Failed
    public List<string> ImpactedModules { get; set; } = new();
    public List<ReplicationDetailDto> Details { get; set; } = new();
}

public sealed class ReplicationDetailDto
{
    public string Module { get; set; } = string.Empty; // EFSourceMapping | CalculatedThroughputSetup | DataInput
    public string Status { get; set; } = string.Empty; // Completed | Failed
    public string? Message { get; set; }
}

public sealed class AssetAggregateDto
{
    public string? AssetId { get; set; }

    /// <summary>
    /// Echoed for convenience; server is authoritative from request siteId.
    /// </summary>
    public string? SiteId { get; set; }

    public string? AssetGroup { get; set; }
    public string? ProcessGroup { get; set; }
    public string? ProcessGroupOtherText { get; set; }

    public string? AssetName { get; set; }
    public string? PermitEuId { get; set; }
    public string? GlobalUniqueAssetId { get; set; }
    public string? Description { get; set; }
    public bool? StationaryFlag { get; set; }

    public PseudoChildMappingDto? PseudoChildMapping { get; set; }

    public List<StatusLogDto> StatusLog { get; set; } = new();
    public List<AdditionalAssetIdDto> AdditionalAssetIds { get; set; } = new();
    public List<AssetPropertyDto> AssetProperties { get; set; } = new();
    public List<ControlDeviceMappingDto> ControlDevices { get; set; } = new();
    public List<InputParameterDto> InputParameters { get; set; } = new();
    public List<ParentInputMappingDto> ParentInputMappings { get; set; } = new();
    public List<ReportingAttributeDto> ReportingAttributes { get; set; } = new();
}

public sealed class PseudoChildMappingDto
{
    public string? Mode { get; set; } // None | Parent | Child
    public string? ParentPseudoAssetId { get; set; }
}

public sealed class StatusLogDto
{
    public string? StatusLogId { get; set; }
    public string? OperatingStatus { get; set; } // Active | Inactive
    public string? FromDate { get; set; } // date string
    public string? ToDate { get; set; }   // date string
    public string? Comments { get; set; }
}

public sealed class AdditionalAssetIdDto
{
    public string? AdditionalAssetIdId { get; set; }
    public string? IdType { get; set; }
    public string? IdValue { get; set; }
    public bool InUse { get; set; }
}

public sealed class AssetPropertyDto
{
    public string? AssetPropertyId { get; set; }
    public string? PropertyName { get; set; }
    public string? PropertyValue { get; set; }
    public string? FromDate { get; set; }
    public string? Notes { get; set; }
    public bool InUse { get; set; }
}

public sealed class ControlDeviceMappingDto
{
    public string? ControlDeviceMappingId { get; set; }
    public string? ControlDeviceRefId { get; set; }
    public bool InUse { get; set; }
}

public sealed class InputParameterDto
{
    public string? InputParameterId { get; set; }
    public string? Name { get; set; }
    public string? UomId { get; set; }
    public List<string> ReportingProgramIds { get; set; } = new();
    public string? InputType { get; set; }
    public string? DataEntryFrequency { get; set; }
    public FuelMappingDto? FuelMapping { get; set; }
    public bool InUse { get; set; }
}

public sealed class FuelMappingDto
{
    public string? FuelType { get; set; }
    public string? MappingId { get; set; }
}

public sealed class ParentInputMappingDto
{
    public string? ChildInputId { get; set; }
    public string? ParentInputId { get; set; }
}

public sealed class ReportingAttributeDto
{
    public string? ReportingAttributeId { get; set; }
    public string? AttributeName { get; set; }
    public string? AttributeValue { get; set; }
    public string? ReportingProgramId { get; set; }
    public bool InUse { get; set; }
}

/// <summary>
/// List site assets (summary list) response.
/// </summary>
public sealed class GetSiteAssetsResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public List<AssetListItemDto> Assets { get; set; } = new();
}

public sealed class AssetListItemDto
{
    public string AssetId { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string PermitEuId { get; set; } = string.Empty;
    public string? GlobalUniqueAssetId { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Asset aggregate detail response for edit/copy hydration.
/// </summary>
public sealed class GetSiteAssetByIdResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public AssetAggregateDto Asset { get; set; } = new();
}

/// <summary>
/// Delete request.
/// </summary>
public sealed class RemoveSiteAssetRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public bool Confirm { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeleteMode? DeleteMode { get; set; } = DemoAssetDotnetApi.Api.SiteAssets.DeleteMode.Soft;
    public string? Reason { get; set; }
}

public sealed class RemoveSiteAssetResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public RemoveSiteAssetResultDto Result { get; set; } = new();
}

public sealed class RemoveSiteAssetResultDto
{
    public string Status { get; set; } = "Deleted"; // Deleted | Blocked
    public string Message { get; set; } = string.Empty;
    public List<DeleteBlockedByDto> BlockedBy { get; set; } = new();
}

public sealed class DeleteBlockedByDto
{
    public string DependencyType { get; set; } = string.Empty;
    public string DependencyId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Prepare Copy request (BRD-aligned): returns confirmation payload and hydrated source snapshot.
/// </summary>
public sealed class PrepareCopyRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string SourceAssetId { get; set; } = string.Empty;

    /// <summary>
    /// Optional reporting year used for linked-module hydration in the full BRD design.
    /// Not used by the current in-memory demo persistence.
    /// </summary>
    public int? ReportingYear { get; set; }
}

public sealed class PrepareCopyResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public CopyConfirmationDto Confirmation { get; set; } = new();
    public HydratedSourceSnapshotDto HydratedSource { get; set; } = new();
}

/// <summary>
/// Confirmation payload used by the UI to render the "Confirmation!" dialog (FR-03).
/// </summary>
public sealed class CopyConfirmationDto
{
    public string AssetName { get; set; } = string.Empty;
    public string PermitEuId { get; set; } = string.Empty;

    /// <summary>
    /// Status date displayed in confirmation (derived from status log; can be empty if unknown).
    /// </summary>
    public string StatusDate { get; set; } = string.Empty;
}

/// <summary>
/// Hydrated snapshot of source state used for downstream copy save and replication orchestration.
/// For POC, this only includes the asset aggregate; linked-module payloads are stubbed for forward-compat.
/// </summary>
public sealed class HydratedSourceSnapshotDto
{
    public AssetAggregateDto Asset { get; set; } = new();

    // Linked modules (stubs for contract completeness vs CodeWiki build spec).
    public List<object> EfSourceMappings { get; set; } = new();
    public List<object> ThroughputSetup { get; set; } = new();
}
