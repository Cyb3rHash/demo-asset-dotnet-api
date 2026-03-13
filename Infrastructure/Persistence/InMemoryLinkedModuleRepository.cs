using System.Collections.Concurrent;
using DemoAssetDotnetApi.Api.LinkedModules;

namespace DemoAssetDotnetApi.Infrastructure.Persistence;

/// <summary>
/// In-memory persistence for linked modules used by the BRD-aligned API inventory.
/// </summary>
public sealed class InMemoryLinkedModuleRepository : ILinkedModuleRepository
{
    // EF mappings: siteId -> assetId -> mappingId -> dto
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, EfSourceMappingDto>>> _ef =
        new(StringComparer.OrdinalIgnoreCase);

    // Throughput equations: siteId -> reportingYear -> assetId -> equationSetupId -> dto
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, ConcurrentDictionary<string, CalculatedThroughputEquationDto>>>> _throughput =
        new(StringComparer.OrdinalIgnoreCase);

    // Data input values: siteId -> assetId -> year -> inputParameterId -> value
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>> _dataInput =
        new(StringComparer.OrdinalIgnoreCase);

    // Replication outcomes: siteId -> copyOperationId -> outcome
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ReplicationOutcomeDto>> _replication =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<List<EfSourceMappingDto>> ListEfMappingsAsync(string siteId, string? assetId, CancellationToken ct)
    {
        var bySite = _ef.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, EfSourceMappingDto>>(StringComparer.OrdinalIgnoreCase));

        IEnumerable<EfSourceMappingDto> query;
        if (!string.IsNullOrWhiteSpace(assetId))
        {
            if (!bySite.TryGetValue(assetId, out var byId))
            {
                query = Array.Empty<EfSourceMappingDto>();
            }
            else
            {
                query = byId.Values;
            }
        }
        else
        {
            query = bySite.Values.SelectMany(v => v.Values);
        }

        var list = query
            .Select(CloneEf)
            .OrderBy(x => x.AssetId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InputParameterId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(list);
    }

    public Task UpsertEfMappingsAsync(string siteId, string assetId, List<EfSourceMappingDto> mappings, CancellationToken ct)
    {
        var bySite = _ef.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, EfSourceMappingDto>>(StringComparer.OrdinalIgnoreCase));
        var byAsset = bySite.GetOrAdd(assetId, _ => new ConcurrentDictionary<string, EfSourceMappingDto>(StringComparer.OrdinalIgnoreCase));

        foreach (var m in mappings)
        {
            var id = string.IsNullOrWhiteSpace(m.MappingId) ? Guid.NewGuid().ToString("D") : m.MappingId!;
            var stored = CloneEf(m);
            stored.MappingId = id;
            stored.AssetId = assetId; // authoritative
            byAsset[id] = stored;
        }

        return Task.CompletedTask;
    }

    public Task<List<CalculatedThroughputEquationDto>> ListThroughputEquationsAsync(string siteId, string? assetId, int reportingYear, CancellationToken ct)
    {
        var bySite = _throughput.GetOrAdd(siteId, _ => new ConcurrentDictionary<int, ConcurrentDictionary<string, ConcurrentDictionary<string, CalculatedThroughputEquationDto>>>());
        var byYear = bySite.GetOrAdd(reportingYear, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, CalculatedThroughputEquationDto>>(StringComparer.OrdinalIgnoreCase));

        IEnumerable<CalculatedThroughputEquationDto> query;
        if (!string.IsNullOrWhiteSpace(assetId))
        {
            if (!byYear.TryGetValue(assetId, out var byEq))
            {
                query = Array.Empty<CalculatedThroughputEquationDto>();
            }
            else
            {
                query = byEq.Values;
            }
        }
        else
        {
            query = byYear.Values.SelectMany(v => v.Values);
        }

        var list = query
            .Select(CloneEq)
            .OrderBy(x => x.AssetId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InputParameterId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(list);
    }

    public Task UpsertThroughputEquationsBulkAsync(string siteId, int reportingYear, List<CalculatedThroughputEquationDto> equations, CancellationToken ct)
    {
        // Bulk upsert by grouping per asset.
        foreach (var grp in equations.GroupBy(e => e.AssetId, StringComparer.OrdinalIgnoreCase))
        {
            UpsertThroughputEquationsAsync(siteId, grp.Key, reportingYear, grp.ToList(), ct);
        }

        return Task.CompletedTask;
    }

    public Task UpsertThroughputEquationsAsync(string siteId, string assetId, int reportingYear, List<CalculatedThroughputEquationDto> equations, CancellationToken ct)
    {
        var bySite = _throughput.GetOrAdd(siteId, _ => new ConcurrentDictionary<int, ConcurrentDictionary<string, ConcurrentDictionary<string, CalculatedThroughputEquationDto>>>());
        var byYear = bySite.GetOrAdd(reportingYear, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, CalculatedThroughputEquationDto>>(StringComparer.OrdinalIgnoreCase));
        var byAsset = byYear.GetOrAdd(assetId, _ => new ConcurrentDictionary<string, CalculatedThroughputEquationDto>(StringComparer.OrdinalIgnoreCase));

        foreach (var e in equations)
        {
            var id = string.IsNullOrWhiteSpace(e.EquationSetupId) ? Guid.NewGuid().ToString("D") : e.EquationSetupId!;
            var stored = CloneEq(e);
            stored.EquationSetupId = id;
            stored.AssetId = assetId;
            byAsset[id] = stored;
        }

        return Task.CompletedTask;
    }

    public Task<List<int>> ListThroughputReportingYearsAsync(string siteId, string assetId, CancellationToken ct)
    {
        var bySite = _throughput.GetOrAdd(siteId, _ => new ConcurrentDictionary<int, ConcurrentDictionary<string, ConcurrentDictionary<string, CalculatedThroughputEquationDto>>>());

        var years = bySite
            .Where(kvp => kvp.Value.ContainsKey(assetId))
            .Select(kvp => kvp.Key)
            .OrderBy(x => x)
            .ToList();

        return Task.FromResult(years);
    }

    public Task UpsertDataInputValueAsync(string siteId, UpsertDataInputValueRequest request, CancellationToken ct)
    {
        var bySite = _dataInput.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>(StringComparer.OrdinalIgnoreCase));
        var byAsset = bySite.GetOrAdd(request.AssetId, _ => new ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>());
        var byYear = byAsset.GetOrAdd(request.ReportingYear, _ => new ConcurrentDictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));

        byYear[request.InputParameterId] = request.Value;
        return Task.CompletedTask;
    }

    public Task<List<StoredDataInputValue>> ListDataInputValuesAsync(string siteId, string assetId, CancellationToken ct)
    {
        var bySite = _dataInput.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>(StringComparer.OrdinalIgnoreCase));
        if (!bySite.TryGetValue(assetId, out var byYear))
        {
            return Task.FromResult(new List<StoredDataInputValue>());
        }

        var list = byYear
            .SelectMany(year => year.Value.Select(v => new StoredDataInputValue
            {
                AssetId = assetId,
                ReportingYear = year.Key,
                InputParameterId = v.Key,
                Value = v.Value
            }))
            .OrderBy(x => x.ReportingYear)
            .ThenBy(x => x.InputParameterId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(list);
    }

    public Task UpsertReplicationOutcomeAsync(string siteId, ReplicationOutcomeDto outcome, CancellationToken ct)
    {
        var bySite = _replication.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, ReplicationOutcomeDto>(StringComparer.OrdinalIgnoreCase));
        bySite[outcome.CopyOperationId] = CloneOutcome(outcome);
        return Task.CompletedTask;
    }

    public Task<ReplicationOutcomeDto?> GetReplicationOutcomeAsync(string siteId, string copyOperationId, CancellationToken ct)
    {
        var bySite = _replication.GetOrAdd(siteId, _ => new ConcurrentDictionary<string, ReplicationOutcomeDto>(StringComparer.OrdinalIgnoreCase));
        return Task.FromResult(bySite.TryGetValue(copyOperationId, out var v) ? CloneOutcome(v) : null);
    }

    private static EfSourceMappingDto CloneEf(EfSourceMappingDto src)
    {
        return new EfSourceMappingDto
        {
            MappingId = src.MappingId,
            AssetId = src.AssetId,
            InputParameterId = src.InputParameterId,
            EfSourceSet = src.EfSourceSet,
            EfSourceTable = src.EfSourceTable,
            EquationExpression = src.EquationExpression,
            ReportingProgramIds = src.ReportingProgramIds.ToList()
        };
    }

    private static CalculatedThroughputEquationDto CloneEq(CalculatedThroughputEquationDto src)
    {
        return new CalculatedThroughputEquationDto
        {
            EquationSetupId = src.EquationSetupId,
            AssetId = src.AssetId,
            InputParameterId = src.InputParameterId,
            MasterEquationId = src.MasterEquationId,
            GeneratedEquation = src.GeneratedEquation,
            Scalars = src.Scalars.Select(s => new EquationScalarDto
            {
                ScalarId = s.ScalarId,
                ScalarType = s.ScalarType,
                ScalarRefId = s.ScalarRefId,
                ScalarValue = s.ScalarValue
            }).ToList()
        };
    }

    private static ReplicationOutcomeDto CloneOutcome(ReplicationOutcomeDto src)
    {
        return new ReplicationOutcomeDto
        {
            CopyOperationId = src.CopyOperationId,
            SiteId = src.SiteId,
            SourceAssetId = src.SourceAssetId,
            TargetAssetId = src.TargetAssetId,
            Status = src.Status,
            ImpactedModules = src.ImpactedModules.ToList(),
            Details = src.Details.Select(d => new ReplicationModuleResultDto
            {
                Module = d.Module,
                Status = d.Status,
                Message = d.Message
            }).ToList(),
            TimestampUtc = src.TimestampUtc,
            ReasonCode = src.ReasonCode
        };
    }
}
