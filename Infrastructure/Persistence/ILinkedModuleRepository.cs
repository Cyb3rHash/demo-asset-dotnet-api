using DemoAssetDotnetApi.Api.LinkedModules;

namespace DemoAssetDotnetApi.Infrastructure.Persistence;

public interface ILinkedModuleRepository
{
    // EF Source Mapping
    Task<List<EfSourceMappingDto>> ListEfMappingsAsync(string siteId, string? assetId, CancellationToken ct);
    Task UpsertEfMappingsAsync(string siteId, string assetId, List<EfSourceMappingDto> mappings, CancellationToken ct);

    // Calculated Throughput Setup
    Task<List<CalculatedThroughputEquationDto>> ListThroughputEquationsAsync(string siteId, string? assetId, int reportingYear, CancellationToken ct);

    Task UpsertThroughputEquationsBulkAsync(string siteId, int reportingYear, List<CalculatedThroughputEquationDto> equations, CancellationToken ct);

    Task UpsertThroughputEquationsAsync(string siteId, string assetId, int reportingYear, List<CalculatedThroughputEquationDto> equations, CancellationToken ct);

    Task<List<int>> ListThroughputReportingYearsAsync(string siteId, string assetId, CancellationToken ct);

    // Data Input (minimal)
    Task UpsertDataInputValueAsync(string siteId, UpsertDataInputValueRequest request, CancellationToken ct);
    Task<List<StoredDataInputValue>> ListDataInputValuesAsync(string siteId, string assetId, CancellationToken ct);

    // Replication outcomes
    Task UpsertReplicationOutcomeAsync(string siteId, ReplicationOutcomeDto outcome, CancellationToken ct);
    Task<ReplicationOutcomeDto?> GetReplicationOutcomeAsync(string siteId, string copyOperationId, CancellationToken ct);
}

/// <summary>
/// Minimal stored record for data input values (repository internal DTO).
/// </summary>
public sealed class StoredDataInputValue
{
    public string AssetId { get; set; } = string.Empty;
    public int ReportingYear { get; set; }
    public string InputParameterId { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
