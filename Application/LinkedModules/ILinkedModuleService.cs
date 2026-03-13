using DemoAssetDotnetApi.Api.LinkedModules;

namespace DemoAssetDotnetApi.Application.LinkedModules;

public interface ILinkedModuleService
{
    // EF Source Mapping
    Task<GetEfSourceMappingsForSiteResponse> GetEfMappingsAsync(GetEfSourceMappingsForSiteRequest request, CancellationToken ct);
    Task<ManageEfSourceMappingsResponse> UpsertEfMappingsAsync(ManageEfSourceMappingsRequest request, CancellationToken ct);

    // Calculated Throughput
    Task<GetCalculatedThroughputInputsForSiteResponse> GetThroughputEquationsAsync(GetCalculatedThroughputInputsForSiteRequest request, CancellationToken ct);
    Task<ManageCalculatedThroughputInputsResponse> UpsertThroughputEquationsAsync(ManageCalculatedThroughputInputsRequest request, CancellationToken ct);
    Task<GenerateThroughputForInputParameterResponse> GenerateThroughputAsync(GenerateThroughputForInputParameterRequest request, CancellationToken ct);

    // Data Input (minimal)
    Task<UpsertDataInputValueResponse> UpsertDataInputValueAsync(UpsertDataInputValueRequest request, CancellationToken ct);
}
