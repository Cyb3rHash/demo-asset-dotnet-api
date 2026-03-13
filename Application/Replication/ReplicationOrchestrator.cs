using DemoAssetDotnetApi.Api.LinkedModules;
using DemoAssetDotnetApi.Api.SiteAssets;
using DemoAssetDotnetApi.Infrastructure.Persistence;

namespace DemoAssetDotnetApi.Application.Replication;

/// <summary>
/// Orchestrates post-save replication after Copy.
/// Flow name: AssetCopyReplicationFlow.
/// Single entrypoint: ReplicateAfterCopyAsync.
/// </summary>
public sealed class ReplicationOrchestrator
{
    private readonly ILinkedModuleRepository _linkedRepo;
    private readonly ILogger<ReplicationOrchestrator> _logger;

    public ReplicationOrchestrator(ILinkedModuleRepository linkedRepo, ILogger<ReplicationOrchestrator> logger)
    {
        _linkedRepo = linkedRepo;
        _logger = logger;
    }

    // PUBLIC_INTERFACE
    public async Task<ReplicationOutcomeDto> ReplicateAfterCopyAsync(
        string siteId,
        CopyLineageDto lineage,
        CancellationToken ct)
    {
        """
        Post-copy replication orchestrator.

        Contract:
        - Inputs: siteId, lineage (must include copyOperationId, sourceAssetId, targetAssetId, timestampUtc)
        - Outputs: persisted ReplicationOutcomeDto with Status: Completed|Partial|Failed and per-module details.
        - Errors: throws only on unexpected system failures; module failures are captured as Partial/Failed details.
        - Side effects: persists EF mappings, throughput equation setups, and minimal Data Input values to the linked-module repository.

        BRD link:
        - Replication Result Status must be Completed/Partial/Failed with impacted module list (BRD §6.13).
        """;

        // REQ: BRD-FR-03 - Copy Save must proceed save + replication and return transparent progress/outcome.
        _logger.LogInformation(
            "AssetCopyReplicationFlow start. siteId={SiteId} copyOperationId={CopyOperationId} sourceAssetId={SourceAssetId} targetAssetId={TargetAssetId}",
            siteId,
            lineage.CopyOperationId,
            lineage.SourceAssetId,
            lineage.TargetAssetId);

        var details = new List<ReplicationModuleResultDto>();

        // Step 1: EF Source Mapping replication
        details.Add(await ReplicateEfMappingsAsync(siteId, lineage.SourceAssetId, lineage.TargetAssetId, ct));

        // Step 2: Calculated Throughput Setup replication
        details.Add(await ReplicateThroughputSetupAsync(siteId, lineage.SourceAssetId, lineage.TargetAssetId, ct));

        // Step 3: Minimal Data Input replication (optional, but included for POC completeness)
        details.Add(await ReplicateDataInputAsync(siteId, lineage.SourceAssetId, lineage.TargetAssetId, ct));

        var impacted = details
            .Where(d => d.Status != ReplicationStatus.NotApplicable)
            .Select(d => d.Module)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var status = Aggregate(details);

        var outcome = new ReplicationOutcomeDto
        {
            CopyOperationId = lineage.CopyOperationId,
            SiteId = siteId,
            SourceAssetId = lineage.SourceAssetId,
            TargetAssetId = lineage.TargetAssetId,
            Status = status,
            ImpactedModules = impacted,
            Details = details,
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            ReasonCode = status switch
            {
                ReplicationStatus.Completed => null,
                ReplicationStatus.Partial => "ONE_OR_MORE_MODULES_FAILED",
                ReplicationStatus.Failed => "ALL_MODULES_FAILED",
                _ => null
            }
        };

        await _linkedRepo.UpsertReplicationOutcomeAsync(siteId, outcome, ct);

        _logger.LogInformation(
            "AssetCopyReplicationFlow end. siteId={SiteId} copyOperationId={CopyOperationId} status={Status}",
            siteId,
            lineage.CopyOperationId,
            status);

        return outcome;
    }

    private static ReplicationStatus Aggregate(List<ReplicationModuleResultDto> details)
    {
        var applicable = details.Where(d => d.Status != ReplicationStatus.NotApplicable).ToList();
        if (applicable.Count == 0) return ReplicationStatus.NotApplicable;
        if (applicable.All(d => d.Status == ReplicationStatus.Completed)) return ReplicationStatus.Completed;
        if (applicable.All(d => d.Status == ReplicationStatus.Failed)) return ReplicationStatus.Failed;
        return ReplicationStatus.Partial;
    }

    private async Task<ReplicationModuleResultDto> ReplicateEfMappingsAsync(string siteId, string sourceAssetId, string targetAssetId, CancellationToken ct)
    {
        try
        {
            // REQ: BRD-Linked-001 - Replicate EF mappings post-save to matched target inputs.
            var sourceMappings = await _linkedRepo.ListEfMappingsAsync(siteId, sourceAssetId, ct);

            var cloned = sourceMappings
                .Select(m => new EfSourceMappingDto
                {
                    MappingId = null, // generate new
                    AssetId = targetAssetId,
                    InputParameterId = m.InputParameterId,
                    EfSourceSet = m.EfSourceSet,
                    EfSourceTable = m.EfSourceTable,
                    EquationExpression = m.EquationExpression,
                    ReportingProgramIds = m.ReportingProgramIds.ToList()
                })
                .ToList();

            await _linkedRepo.UpsertEfMappingsAsync(siteId, targetAssetId, cloned, ct);

            return new ReplicationModuleResultDto
            {
                Module = LinkedModuleContracts.Modules.EfSourceMapping,
                Status = ReplicationStatus.Completed,
                Message = $"Replicated {cloned.Count} EF mapping(s)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EF Source Mapping replication failed. siteId={SiteId} sourceAssetId={SourceAssetId} targetAssetId={TargetAssetId}", siteId, sourceAssetId, targetAssetId);
            return new ReplicationModuleResultDto
            {
                Module = LinkedModuleContracts.Modules.EfSourceMapping,
                Status = ReplicationStatus.Failed,
                Message = "EF Source Mapping replication failed."
            };
        }
    }

    private async Task<ReplicationModuleResultDto> ReplicateThroughputSetupAsync(string siteId, string sourceAssetId, string targetAssetId, CancellationToken ct)
    {
        try
        {
            // REQ: BRD-Linked-002 - Replicate Calculated Throughput equations post-save.
            // POC assumption: replicate all years present for the source asset.
            var years = await _linkedRepo.ListThroughputReportingYearsAsync(siteId, sourceAssetId, ct);

            int total = 0;
            foreach (var year in years)
            {
                var sourceEquations = await _linkedRepo.ListThroughputEquationsAsync(siteId, sourceAssetId, year, ct);

                var cloned = sourceEquations
                    .Select(e => new CalculatedThroughputEquationDto
                    {
                        EquationSetupId = null, // new
                        AssetId = targetAssetId,
                        InputParameterId = e.InputParameterId,
                        MasterEquationId = e.MasterEquationId,
                        GeneratedEquation = e.GeneratedEquation,
                        Scalars = e.Scalars.Select(s => new EquationScalarDto
                        {
                            ScalarId = null,
                            ScalarType = s.ScalarType,
                            ScalarRefId = s.ScalarRefId,
                            ScalarValue = s.ScalarValue
                        }).ToList()
                    })
                    .ToList();

                await _linkedRepo.UpsertThroughputEquationsAsync(siteId, targetAssetId, year, cloned, ct);
                total += cloned.Count;
            }

            return new ReplicationModuleResultDto
            {
                Module = LinkedModuleContracts.Modules.CalculatedThroughputSetup,
                Status = ReplicationStatus.Completed,
                Message = $"Replicated {total} throughput equation setup(s) across {years.Count} year(s)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calculated Throughput replication failed. siteId={SiteId} sourceAssetId={SourceAssetId} targetAssetId={TargetAssetId}", siteId, sourceAssetId, targetAssetId);
            return new ReplicationModuleResultDto
            {
                Module = LinkedModuleContracts.Modules.CalculatedThroughputSetup,
                Status = ReplicationStatus.Failed,
                Message = "Calculated Throughput replication failed."
            };
        }
    }

    private async Task<ReplicationModuleResultDto> ReplicateDataInputAsync(string siteId, string sourceAssetId, string targetAssetId, CancellationToken ct)
    {
        try
        {
            // REQ: BRD-Linked-003 - Data Input depends on configured inputs and throughput mapping outputs.
            // POC: replicate any stored numeric values for the source asset.
            var values = await _linkedRepo.ListDataInputValuesAsync(siteId, sourceAssetId, ct);

            var clonedCount = 0;
            foreach (var v in values)
            {
                await _linkedRepo.UpsertDataInputValueAsync(
                    siteId,
                    new UpsertDataInputValueRequest
                    {
                        SiteId = siteId,
                        AssetId = targetAssetId,
                        ReportingYear = v.ReportingYear,
                        InputParameterId = v.InputParameterId,
                        Value = v.Value
                    },
                    ct);

                clonedCount++;
            }

            return new ReplicationModuleResultDto
            {
                Module = LinkedModuleContracts.Modules.DataInput,
                Status = ReplicationStatus.Completed,
                Message = $"Replicated {clonedCount} data input value(s)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data Input replication failed. siteId={SiteId} sourceAssetId={SourceAssetId} targetAssetId={TargetAssetId}", siteId, sourceAssetId, targetAssetId);
            return new ReplicationModuleResultDto
            {
                Module = LinkedModuleContracts.Modules.DataInput,
                Status = ReplicationStatus.Failed,
                Message = "Data Input replication failed."
            };
        }
    }
}
