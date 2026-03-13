#!/usr/bin/env bash
set -euo pipefail

# Demo Asset .NET API - one-shot restore/build/run script
# - Designed for cloud/container environments where the platform provides $PORT
# - Runs from the correct folder (this script's directory)
# - Emits clear diagnostics to troubleshoot "502 / not sure where it's running" issues

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "============================================================"
echo "Demo Asset .NET API :: restore/build/run"
echo "Working directory : $SCRIPT_DIR"
echo "Date (UTC)        : $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "User              : $(id -u):$(id -g) ($(whoami || true))"
echo "OS                : $(uname -a)"
echo "============================================================"
echo

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet CLI not found in PATH."
  echo "Make sure the environment has .NET SDK 8 installed."
  exit 127
fi

echo "dotnet --info"
dotnet --info || true
echo

PROJECT_FILE="${PROJECT_FILE:-DemoAssetDotnetApi.csproj}"
if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "ERROR: Project file not found: $SCRIPT_DIR/$PROJECT_FILE"
  echo "Set PROJECT_FILE env var if the csproj has a different name/path."
  echo "Contents of directory:"
  ls -la
  exit 2
fi

# Cloud platforms typically inject PORT. If not present, default to 8080.
export PORT="${PORT:-8080}"

# ASP.NET Core also honors ASPNETCORE_URLS; set it explicitly for clarity.
# Program.cs already reads PORT and adds http://0.0.0.0:${PORT}, but we keep this as an extra
# safety net and to make it obvious in logs what the bind address is.
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT}}"

# Make sure we run in Production unless explicitly overridden.
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"

echo "Configuration:"
echo "  PROJECT_FILE           = $PROJECT_FILE"
echo "  ASPNETCORE_ENVIRONMENT = $ASPNETCORE_ENVIRONMENT"
echo "  PORT                   = $PORT"
echo "  ASPNETCORE_URLS        = $ASPNETCORE_URLS"
echo

echo "Step 1/3: dotnet restore"
dotnet restore "$PROJECT_FILE"
echo

echo "Step 2/3: dotnet build (Release)"
dotnet build "$PROJECT_FILE" -c Release --no-restore
echo

echo "Step 3/3: dotnet run (Release)"
echo "If the environment does health checks, try:"
echo "  GET  /health"
echo "  GET  /swagger (if enabled)"
echo

# Use --no-build to avoid rebuilding after our explicit build step.
exec dotnet run --project "$PROJECT_FILE" -c Release --no-build
