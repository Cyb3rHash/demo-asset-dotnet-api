using DemoAssetDotnetApi.Application.Assets;
using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DemoAssetDotnetApi.Api.SiteAssets;

[ApiController]
[Route("api/siteassets")]
[Produces("application/json")]
public sealed class SiteAssetsController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly ILogger<SiteAssetsController> _logger;

    public SiteAssetsController(IAssetService assetService, ILogger<SiteAssetsController> logger)
    {
        _assetService = assetService;
        _logger = logger;
    }

    /// <summary>
    /// Save asset aggregate (Add/Edit/Copy).
    /// BRD observed path: POST api/siteassets/managesiteassets
    /// </summary>
    /// <remarks>
    /// Status code mapping:
    /// - 200: success
    /// - 400: validation error (category=Validation)
    /// - 409: business conflict (category=Business)
    /// - 500: system error (category=System)
    /// </remarks>
    [HttpPost("managesiteassets")]
    [ProducesResponseType(typeof(ManageSiteAssetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManageSiteAssetsResponse>> ManageSiteAssets([FromBody] ManageSiteAssetsRequest request, CancellationToken ct)
    {
        // REQ: REQ-007 - Provide BRD observed endpoint paths (compatibility surface).
        // REQ: REQ-004 - Validation pipeline (FluentValidation/MVC) blocks invalid payloads and returns envelope.
        _logger.LogInformation(
            "ManageSiteAssets called. operation={Operation} siteId={SiteId} assetId={AssetId}",
            request.Operation,
            request.SiteId,
            request.Asset.AssetId);

        var result = await _assetService.ManageAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// List assets for a site (supports basic filtering).
    /// BRD observed path: GET api/siteassets/getsiteassets?siteId=...
    /// </summary>
    /// <param name="siteId">Required site identifier.</param>
    /// <param name="search">Optional free-text search (assetName, permitEuId, globalUniqueAssetId).</param>
    /// <param name="includeDeleted">If true, include soft-deleted assets.</param>
    [HttpGet("getsiteassets")]
    [ProducesResponseType(typeof(GetSiteAssetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetSiteAssetsResponse>> GetSiteAssets(
        [FromQuery] string siteId,
        [FromQuery] string? search,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var response = await _assetService.ListAsync(siteId, search, includeDeleted, ct);
        return Ok(response);
    }

    /// <summary>
    /// Retrieve a full asset aggregate by id for edit/copy hydration.
    /// Path per CodeWiki build spec: GET api/siteassets/getsiteassetbyid?siteId=...&amp;assetId=...
    /// </summary>
    [HttpGet("getsiteassetbyid")]
    [ProducesResponseType(typeof(GetSiteAssetByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetSiteAssetByIdResponse>> GetSiteAssetById(
        [FromQuery] string siteId,
        [FromQuery] string assetId,
        CancellationToken ct)
    {
        var response = await _assetService.GetByIdAsync(siteId, assetId, ct);
        return Ok(response);
    }

    /// <summary>
    /// Delete an asset (soft delete preferred).
    /// BRD observed path: POST api/siteassets/removesiteasset
    /// </summary>
    /// <remarks>
    /// Requires confirm=true to proceed. deleteMode defaults to Soft.
    /// </remarks>
    [HttpPost("removesiteasset")]
    [ProducesResponseType(typeof(RemoveSiteAssetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RemoveSiteAssetResponse>> RemoveSiteAsset([FromBody] RemoveSiteAssetRequest request, CancellationToken ct)
    {
        var response = await _assetService.RemoveAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Prepare Copy: returns the BRD-required confirmation payload ("Confirmation!" dialog) and a hydrated source snapshot.
    /// BRD observed/recommended path: POST api/siteassets/preparecopy
    /// </summary>
    /// <remarks>
    /// This endpoint is intentionally non-mutating. It supports the FR-03 Copy workflow where the user can:
    /// 1) request confirmation data, 2) choose Edit (no save), or 3) choose Save (then call managesiteassets operation=Copy).
    /// </remarks>
    [HttpPost("preparecopy")]
    [ProducesResponseType(typeof(PrepareCopyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PrepareCopyResponse>> PrepareCopy([FromBody] PrepareCopyRequest request, CancellationToken ct)
    {
        // REQ: FR-03 - Provide confirmation payload and hydrated snapshot pre-save.
        _logger.LogInformation(
            "PrepareCopy called. siteId={SiteId} sourceAssetId={SourceAssetId} reportingYear={ReportingYear}",
            request.SiteId,
            request.SourceAssetId,
            request.ReportingYear);

        var response = await _assetService.PrepareCopyAsync(request, ct);
        return Ok(response);
    }
}
