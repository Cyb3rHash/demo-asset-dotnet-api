# demo-asset-dotnet-api — Environment Variables Manifest

This document is the **container-level environment manifest** for the `demo-asset-dotnet-api` service.

It describes:
- Which environment variables are **required** vs **optional**
- The **source of truth** for variables used by this container
- How variables **map to runtime configuration** in the .NET application

> IMPORTANT:
> - Do **not** commit real secrets or a real `.env` to the repo.
> - Use `.env.example` as the canonical list of variables supported by this container.
> - The orchestration/deployment system should provide the real values at runtime.

## Source of truth

The source of truth for this container’s environment keys is:

- `.env.example`

## Variables

### `PORT` (optional)

**Type:** integer (string in environment)  
**Default:** not set (ASP.NET Core default hosting behavior applies)  
**Example:** `PORT=8080`

**What it does:**
- When set, the API binds Kestrel to `http://0.0.0.0:{PORT}` for container-friendly listening.

**Runtime mapping (code path):**
- `Program.cs` reads `Environment.GetEnvironmentVariable("PORT")`
- If present, adds an explicit URL binding:
  - `app.Urls.Add($"http://0.0.0.0:{port}")`

**When to set it:**
- Recommended in containers and PaaS environments where a platform assigns a port.

## Notes on .NET configuration mapping

This service uses standard ASP.NET Core configuration defaults (e.g., `appsettings.json`, `appsettings.{Environment}.json`, plus environment variables).  

Currently, the only explicitly consumed environment variable in code is:

- `PORT`

If additional runtime configuration keys are added later (connection strings, feature flags, etc.), they should be:
1. Added to `.env.example`
2. Documented in this manifest with:
   - required/optional status
   - default
   - mapping to configuration (e.g., `builder.Configuration["SomeKey"]` or `IOptions<T>`)
3. Kept out of the real `.env` in source control

## Related files

- `.env.example` — canonical example env file
- `Program.cs` — contains `PORT` host binding logic
- `appsettings.json` / `appsettings.Development.json` — baseline configuration files
