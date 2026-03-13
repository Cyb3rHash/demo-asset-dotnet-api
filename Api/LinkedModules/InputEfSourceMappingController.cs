using DemoAssetDotnetApi.Api.LinkedModules;
using DemoAssetDotnetApi.Application.LinkedModules;
using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DemoAssetDotnetApi.Api.LinkedModules;

[ApiController]
[Route("api/inputefsourcemapping")]
[Produces("application/json")]
[Tags("EF Source Mapping")]
public sealed class InputEfSourceMappingController : ControllerBase
{
    private readonly ILinkedModuleService _service;
    private readonly ILogger<InputEfSourceMappingController> _logger;

    public InputEfSourceMappingController(ILinkedModuleService service, ILogger<InputEfSourceMappingController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve EF source mappings for a site (optionally filtered by assetId).
    /// BRD observed path: GET api/inputefsourcemapping/getinputefsourcemappingforsite
    /// </summary>
    [HttpGet("getinputefsourcemappingforsite")]
    [SwaggerOperation(
        Summary = "Get EF Source Mappings for site",
        Description = "Returns EF source mappings for the given site. For this POC, a minimal schema is returned; in a full implementation this includes EF source sets/tables and equation/scalar details.")]
    [ProducesResponseType(typeof(GetEfSourceMappingsForSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetEfSourceMappingsForSiteResponse>> GetForSite(
        [FromQuery] string siteId,
        [FromQuery] string? assetId,
        [FromQuery] int? reportingYear,
        CancellationToken ct)
    {
        // REQ: BRD-API-001 - Provide observed endpoint for EF Source Mapping retrieval.
        _logger.LogInformation("Get EF Source Mappings called. siteId={SiteId} assetId={AssetId} reportingYear={ReportingYear}", siteId, assetId, reportingYear);
        var response = await _service.GetEfMappingsAsync(new GetEfSourceMappingsForSiteRequest { SiteId = siteId, AssetId = assetId, ReportingYear = reportingYear }, ct);
        return Ok(response);
    }

    /// <summary>
    /// Upsert EF source mappings for an asset.
    /// BRD observed path: POST api/inputefsourcemapping/manageinputefsourcemapping
    /// </summary>
    [HttpPost("manageinputefsourcemapping")]
    [SwaggerOperation(
        Summary = "Manage EF Source Mappings",
        Description = "Upserts EF source mappings for an asset. Used by copy replication to persist target mappings.")]
    [ProducesResponseType(typeof(ManageEfSourceMappingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManageEfSourceMappingsResponse>> Manage([FromBody] ManageEfSourceMappingsRequest request, CancellationToken ct)
    {
        // REQ: BRD-API-002 - Provide observed endpoint for EF Source Mapping upsert.
        _logger.LogInformation("Manage EF Source Mappings called. siteId={SiteId} assetId={AssetId} count={Count}", request.SiteId, request.AssetId, request.Mappings.Count);
        var response = await _service.UpsertEfMappingsAsync(request, ct);
        return Ok(response);
    }
}
