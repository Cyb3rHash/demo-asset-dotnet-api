using DemoAssetDotnetApi.Application.LinkedModules;
using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DemoAssetDotnetApi.Api.LinkedModules;

[ApiController]
[Route("api/calculatedthroughputequationsetup")]
[Produces("application/json")]
[Tags("Calculated Throughput Setup")]
public sealed class CalculatedThroughputEquationSetupController : ControllerBase
{
    private readonly ILinkedModuleService _service;
    private readonly ILogger<CalculatedThroughputEquationSetupController> _logger;

    public CalculatedThroughputEquationSetupController(ILinkedModuleService service, ILogger<CalculatedThroughputEquationSetupController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve calculated throughput equation setup for a site (optionally filtered by assetId).
    /// BRD observed path: GET api/calculatedthroughputequationsetup/getcalculatedinputparametersforsite
    /// </summary>
    [HttpGet("getcalculatedinputparametersforsite")]
    [SwaggerOperation(
        Summary = "Get calculated throughput inputs for site",
        Description = "Returns throughput equation setups for the given site and reportingYear. In this POC the payload is minimal but supports copy replication and demo recalculation.")]
    [ProducesResponseType(typeof(GetCalculatedThroughputInputsForSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetCalculatedThroughputInputsForSiteResponse>> GetForSite(
        [FromQuery] string siteId,
        [FromQuery] int reportingYear,
        [FromQuery] string? assetId,
        CancellationToken ct)
    {
        // REQ: BRD-API-003 - Provide observed endpoint for Calculated Throughput retrieval.
        _logger.LogInformation("Get Calculated Throughput called. siteId={SiteId} assetId={AssetId} reportingYear={ReportingYear}", siteId, assetId, reportingYear);

        var response = await _service.GetThroughputEquationsAsync(
            new GetCalculatedThroughputInputsForSiteRequest
            {
                SiteId = siteId,
                AssetId = assetId,
                ReportingYear = reportingYear
            },
            ct);

        return Ok(response);
    }

    /// <summary>
    /// Upsert calculated throughput equation setups.
    /// BRD observed path: POST api/calculatedthroughputequationsetup/managecalculatedinputparameters
    /// </summary>
    [HttpPost("managecalculatedinputparameters")]
    [SwaggerOperation(
        Summary = "Manage calculated throughput inputs",
        Description = "Upserts throughput equation setups for a site and reporting year. Used by copy replication to persist target throughput setup.")]
    [ProducesResponseType(typeof(ManageCalculatedThroughputInputsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManageCalculatedThroughputInputsResponse>> Manage([FromBody] ManageCalculatedThroughputInputsRequest request, CancellationToken ct)
    {
        // REQ: BRD-API-004 - Provide observed endpoint for Calculated Throughput upsert.
        _logger.LogInformation("Manage Calculated Throughput called. siteId={SiteId} reportingYear={ReportingYear} count={Count}", request.SiteId, request.ReportingYear, request.Equations.Count);
        var response = await _service.UpsertThroughputEquationsAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Generate throughput value for an input parameter.
    /// BRD observed path: POST api/calculatedthroughputequationsetup/generatethroughputforinputparameter
    /// </summary>
    [HttpPost("generatethroughputforinputparameter")]
    [SwaggerOperation(
        Summary = "Generate throughput for input parameter",
        Description = "Returns a computed throughput value. POC behavior is deterministic and based on configured scalars/equation count; full implementation would run equation evaluation.")]
    [ProducesResponseType(typeof(GenerateThroughputForInputParameterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerateThroughputForInputParameterResponse>> Generate([FromBody] GenerateThroughputForInputParameterRequest request, CancellationToken ct)
    {
        // REQ: BRD-API-005 - Provide observed endpoint for throughput generation.
        _logger.LogInformation(
            "GenerateThroughput called. siteId={SiteId} assetId={AssetId} reportingYear={ReportingYear} inputParameterId={InputParameterId}",
            request.SiteId,
            request.AssetId,
            request.ReportingYear,
            request.InputParameterId);

        var response = await _service.GenerateThroughputAsync(request, ct);
        return Ok(response);
    }
}
