# demo-asset-dotnet-api

ASP.NET Core (.NET 8) Web API baseline for the Demo Asset Management backend.

This container provides an **industry-standard API baseline** aligned to the BRD/CodeWiki specs:
- Layered structure scaffolding (`Api/`, `Application/`, `Domain/`, `Infrastructure/`)
- Correlation ID middleware (`X-Correlation-Id`)
- Standardized BRD error envelope (`Validation | Business | System`)
- FluentValidation-based validation pipeline
- Request size limits
- Swagger/OpenAPI with status-code mappings and examples

## Prerequisites
- .NET SDK 8.x

## Run locally
```bash
dotnet restore
dotnet run
```

Open:
- Swagger UI: `http://localhost:5024/swagger`
- Health: `http://localhost:5024/health`

## Container-friendly port binding
This API supports the common `PORT` environment variable.

Example:
```bash
PORT=8080 dotnet run
```

## Correlation ID
All requests accept an optional header:

- `X-Correlation-Id: <your-id>`

If missing, the server generates a correlation id and returns it in the same header.

## Standard error envelope (BRD-aligned)
Errors are returned as:

```json
{
  "correlationId": "string",
  "category": "Validation | Business | System",
  "message": "string",
  "errors": [
    {
      "code": "string",
      "tab": "string",
      "fieldPath": "string",
      "message": "string",
      "severity": "Error | Warning"
    }
  ]
}
```

Status code mappings:
- **400**: `category = Validation`
- **409**: `category = Business`
- **500**: `category = System`

## Implemented endpoints

### Asset lifecycle
- `GET /health`
- `POST /api/siteassets/managesiteassets` (Add/Edit/Copy)
- `GET /api/siteassets/getsiteassets?siteId=...`
- `GET /api/siteassets/getsiteassetbyid?siteId=...&assetId=...`
- `POST /api/siteassets/removesiteasset`
- `POST /api/siteassets/preparecopy`

### Linked modules (BRD API inventory)
EF Source Mapping:
- `GET /api/inputefsourcemapping/getinputefsourcemappingforsite?siteId=...&assetId=...`
- `POST /api/inputefsourcemapping/manageinputefsourcemapping`

Calculated Throughput Setup:
- `GET /api/calculatedthroughputequationsetup/getcalculatedinputparametersforsite?siteId=...&reportingYear=...&assetId=...`
- `POST /api/calculatedthroughputequationsetup/managecalculatedinputparameters`
- `POST /api/calculatedthroughputequationsetup/generatethroughputforinputparameter`

Data Input (minimal for POC):
- `POST /api/datainput/upsertvalue`

## Copy + Replication behavior (POC)
On `managesiteassets` with `operation=Copy`:
1. The asset is saved with copy create-semantics (new identity IDs where applicable).
2. A replication orchestrator runs immediately after save to replicate:
   - EF Source Mapping
   - Calculated Throughput Setup
   - Minimal Data Input values

The response includes:
- `copyLineage.replicationResultStatus`: `Completed | Partial | Failed`
- `copyLineage.impactedModules`: modules affected
- `copyLineage.reasonCode`: present when Partial/Failed
- `replication`: per-module details for debugging

## Demo script
Use `DemoAssetDotnetApi.http` (VS Code / Rider HTTP client) to:
- Create asset
- Seed linked-module records
- Copy save (replication runs)
- Verify linked-module replication via GET endpoints
