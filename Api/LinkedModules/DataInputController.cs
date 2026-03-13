using DemoAssetDotnetApi.Application.LinkedModules;
using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DemoAssetDotnetApi.Api.LinkedModules;

[ApiController]
[Route("api/datainput")]
[Produces("application/json")]
[Tags("Data Input")]
public sealed class DataInputController : ControllerBase
{
    private readonly ILinkedModuleService _service;
    private readonly ILogger<DataInputController> _logger;

    public DataInputController(ILinkedModuleService service, ILogger<DataInputController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Minimal Data Input upsert (POC).
    /// This enables persistence of a generated throughput output as an input value.
    /// </summary>
    [HttpPost("upsertvalue")]
    [SwaggerOperation(
        Summary = "Upsert Data Input value (minimal)",
        Description = "Persists a single numeric input value for an asset + inputParameter + reportingYear. Full BRD includes period/frequency; this is the minimal POC-required surface.")]
    [ProducesResponseType(typeof(UpsertDataInputValueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpsertDataInputValueResponse>> Upsert([FromBody] UpsertDataInputValueRequest request, CancellationToken ct)
    {
        // REQ: BRD-API-006 - Provide minimal Data Input endpoint required for POC demonstration.
        _logger.LogInformation(
            "UpsertDataInput called. siteId={SiteId} assetId={AssetId} reportingYear={ReportingYear} inputParameterId={InputParameterId}",
            request.SiteId,
            request.AssetId,
            request.ReportingYear,
            request.InputParameterId);

        var response = await _service.UpsertDataInputValueAsync(request, ct);
        return Ok(response);
    }
}
