using DemoAssetDotnetApi.Api.SiteAssets;
using DemoAssetDotnetApi.Domain.Errors;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DemoAssetDotnetApi.Api.OpenApi;

/// <summary>
/// Injects basic example payloads into OpenAPI schemas for key DTOs.
/// </summary>
public sealed class SwaggerExamplesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(ManageSiteAssetsRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["operation"] = new OpenApiString("Add"),
                ["siteId"] = new OpenApiString("SITE-001"),
                ["asset"] = new OpenApiObject
                {
                    ["assetName"] = new OpenApiString("Boiler 1"),
                    ["permitEuId"] = new OpenApiString("EU-1001"),
                    ["globalUniqueAssetId"] = new OpenApiString("GUID-ASSET-UNIQUE-001"),
                    ["statusLog"] = new OpenApiArray()
                }
            };
        }
        else if (context.Type == typeof(RemoveSiteAssetRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["siteId"] = new OpenApiString("SITE-001"),
                ["assetId"] = new OpenApiString("b6f1e651-6ee2-4f13-9b32-27773d41ae85"),
                ["confirm"] = new OpenApiBoolean(true),
                ["deleteMode"] = new OpenApiString("Soft"),
                ["reason"] = new OpenApiString("User requested removal.")
            };
        }
        else if (context.Type == typeof(GetSiteAssetsResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["correlationId"] = new OpenApiString("demo-corr-002"),
                ["siteId"] = new OpenApiString("SITE-001"),
                ["assets"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["assetId"] = new OpenApiString("b6f1e651-6ee2-4f13-9b32-27773d41ae85"),
                        ["assetName"] = new OpenApiString("Boiler 1"),
                        ["permitEuId"] = new OpenApiString("EU-1001"),
                        ["globalUniqueAssetId"] = new OpenApiString("GUID-ASSET-UNIQUE-001"),
                        ["isDeleted"] = new OpenApiBoolean(false)
                    }
                }
            };
        }
        else if (context.Type == typeof(PrepareCopyRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["siteId"] = new OpenApiString("SITE-001"),
                ["sourceAssetId"] = new OpenApiString("b6f1e651-6ee2-4f13-9b32-27773d41ae85"),
                ["reportingYear"] = new OpenApiInteger(2025)
            };
        }
        else if (context.Type == typeof(PrepareCopyResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["correlationId"] = new OpenApiString("demo-corr-copy-001"),
                ["confirmation"] = new OpenApiObject
                {
                    ["assetName"] = new OpenApiString("Boiler 1"),
                    ["permitEuId"] = new OpenApiString("EU-1001"),
                    ["statusDate"] = new OpenApiString("2025-01-01")
                },
                ["hydratedSource"] = new OpenApiObject
                {
                    ["asset"] = new OpenApiObject
                    {
                        ["assetId"] = new OpenApiString("b6f1e651-6ee2-4f13-9b32-27773d41ae85"),
                        ["siteId"] = new OpenApiString("SITE-001"),
                        ["assetName"] = new OpenApiString("Boiler 1"),
                        ["permitEuId"] = new OpenApiString("EU-1001"),
                        ["globalUniqueAssetId"] = new OpenApiString("GUID-ASSET-UNIQUE-001"),
                        ["statusLog"] = new OpenApiArray()
                    },
                    ["efSourceMappings"] = new OpenApiArray(),
                    ["throughputSetup"] = new OpenApiArray()
                }
            };
        }
        else if (context.Type == typeof(ManageSiteAssetsResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["correlationId"] = new OpenApiString("demo-corr-copy-save-001"),
                ["asset"] = new OpenApiObject
                {
                    ["assetId"] = new OpenApiString("c2f2ed27-7451-4c5d-bd6b-3bb1edb621b3"),
                    ["siteId"] = new OpenApiString("SITE-001"),
                    ["assetName"] = new OpenApiString("Boiler 1 (Copy)"),
                    ["permitEuId"] = new OpenApiString("EU-1001-COPY"),
                    ["globalUniqueAssetId"] = new OpenApiString("GUID-ASSET-UNIQUE-001-COPY"),
                    ["statusLog"] = new OpenApiArray()
                },
                ["copyLineage"] = new OpenApiObject
                {
                    ["copyOperationId"] = new OpenApiString("copyop-demo-001"),
                    ["sourceAssetId"] = new OpenApiString("b6f1e651-6ee2-4f13-9b32-27773d41ae85"),
                    ["targetAssetId"] = new OpenApiString("c2f2ed27-7451-4c5d-bd6b-3bb1edb621b3"),
                    ["timestampUtc"] = new OpenApiString("2026-03-13T00:00:00.0000000Z"),
                    ["performedBy"] = new OpenApiString("demo.user@example.com"),
                    ["replicationResultStatus"] = new OpenApiString("Completed"),
                    ["impactedModules"] = new OpenApiArray
                    {
                        new OpenApiString("EFSourceMapping"),
                        new OpenApiString("CalculatedThroughputSetup"),
                        new OpenApiString("DataInput")
                    }
                },
                ["replication"] = new OpenApiObject
                {
                    ["status"] = new OpenApiString("Completed"),
                    ["impactedModules"] = new OpenApiArray
                    {
                        new OpenApiString("EFSourceMapping"),
                        new OpenApiString("CalculatedThroughputSetup"),
                        new OpenApiString("DataInput")
                    },
                    ["details"] = new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["module"] = new OpenApiString("EFSourceMapping"),
                            ["status"] = new OpenApiString("Completed"),
                            ["message"] = new OpenApiString("Replicated 2 EF mapping(s).")
                        }
                    }
                }
            };
        }
        else if (context.Type == typeof(ErrorResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["correlationId"] = new OpenApiString("7d9d1c2f-2a41-4f4d-8c0d-2d4b7bdb1f61"),
                ["category"] = new OpenApiString("Validation"),
                ["message"] = new OpenApiString("Validation failed."),
                ["errors"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["code"] = new OpenApiString("REQUIRED"),
                        ["tab"] = new OpenApiString("AssetDetails"),
                        ["fieldPath"] = new OpenApiString("asset.assetName"),
                        ["message"] = new OpenApiString("Asset Name is required."),
                        ["severity"] = new OpenApiString("Error")
                    }
                }
            };
        }
    }
}
