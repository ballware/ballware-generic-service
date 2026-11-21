#!/usr/bin/env bash
# Regenerates the OpenAPI snapshots committed in docs/openapi/
# (generic.json, meta.json, service.json, schema.json, manifest.json).
#
# The snapshots are produced offline from the built service assembly via Swashbuckle's
# swagger-to-file tool, using ASPNETCORE_ENVIRONMENT=Swagger (fake Authority/clients,
# no database, no IdP needed). See docs/openapi/README.md for details and the
# live-server fallback.

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: docs/openapi/fetch-spec.sh

Regenerates docs/openapi/{generic,meta,service,schema}.json and docs/openapi/manifest.json.
Run from any location inside the repository; the script resolves the repository root itself.

Prerequisite: 'dotnet restore' against the private ballware NuGet feed, because the
Release build of src/Ballware.Generic.Service cannot be completed without it.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

TOOL_PACKAGE="Swashbuckle.AspNetCore.Cli"
# Pinned to the Swashbuckle.AspNetCore version referenced by the service project
# (src/Ballware.Generic.Service/Ballware.Generic.Service.csproj) so that the committed
# snapshots are formatted identically to the documents the running service serves,
# and identically to the documents CI generates.
TOOL_VERSION="10.3.0"
CONFIG_PROJECT="src/Ballware.Generic.Service"
ASSEMBLY="bin/Release/net10.0/Ballware.Generic.Service.dll"
OUTPUT_DIR="docs/openapi"
DOCUMENTS=(generic meta service schema)

step() { printf '\n==> %s\n' "$*"; }

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is required but was not found on PATH." >&2
  exit 1
fi

step "Building $CONFIG_PROJECT (Release)"
dotnet build "$CONFIG_PROJECT" --configuration Release

step "Ensuring $TOOL_PACKAGE $TOOL_VERSION is available"
# Global install guarded by a PATH check. Alternative (what CI does, see
# .github/workflows/publish-packages.yml):
#   dotnet new tool-manifest || true
#   dotnet tool update Swashbuckle.AspNetCore.Cli --version 10.3.0
# and invoke via: dotnet tool run swagger tofile ...
if command -v swagger >/dev/null 2>&1; then
  installed_version="$(dotnet tool list --global 2>/dev/null \
    | awk 'tolower($1) == "swashbuckle.aspnetcore.cli" { print $2 }')"
  echo "swagger tool: $(command -v swagger) (version ${installed_version:-unknown})"
  if [[ -n "$installed_version" && "$installed_version" != "$TOOL_VERSION" ]]; then
    echo "warning: installed Swashbuckle.AspNetCore.Cli is $installed_version, expected $TOOL_VERSION;" >&2
    echo "warning: formatting of the generated documents may drift. Update with:" >&2
    echo "warning:   dotnet tool update $TOOL_PACKAGE --version $TOOL_VERSION --global" >&2
  fi
else
  dotnet tool install "$TOOL_PACKAGE" --version "$TOOL_VERSION" --global
  export PATH="$HOME/.dotnet/tools:$PATH"
  if ! command -v swagger >/dev/null 2>&1; then
    echo "swagger tool not found after install; add ~/.dotnet/tools to PATH or use the tool-manifest variant." >&2
    exit 1
  fi
fi

step "Generating documents"
# ASPNETCORE_ENVIRONMENT=Swagger loads appsettings.Swagger.json (fake Authority/clients).
# appsettings.local.json is applied after the environment file even in the Swagger
# environment (see src/Ballware.Generic.Service/Program.cs), so the OIDC discovery URL
# in the generated documents is pinned to the fake value below via an environment
# override. This keeps developer IdP URLs out of the committed snapshots
# (see docs/openapi/README.md).
export ASPNETCORE_ENVIRONMENT=Swagger
export Authorization__Authority="https://localhost"

# Run from the service project directory: the appsettings files are resolved relative
# to the content root (current working directory), like in CI. The absolute output
# path keeps the snapshots in docs/openapi/ regardless of the invocation directory.
cd "$CONFIG_PROJECT"

for doc in "${DOCUMENTS[@]}"; do
  swagger tofile --output "$REPO_ROOT/$OUTPUT_DIR/$doc.json" "$ASSEMBLY" "$doc"
done

cd "$REPO_ROOT"

step "Validating and writing manifest"
if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required for validation and manifest generation." >&2
  exit 1
fi

service_version="$(git describe --tags --always --dirty 2>/dev/null || echo unknown)"

TOOL_VERSION="$TOOL_VERSION" SERVICE_VERSION="$service_version" DOCUMENTS="${DOCUMENTS[*]}" \
python3 - "$OUTPUT_DIR" <<'PY'
import datetime
import json
import os
import sys

output_dir = sys.argv[1]
manifest = {
    "source": "Swashbuckle.AspNetCore.Cli swagger tofile",
    "toolVersion": os.environ["TOOL_VERSION"],
    "environment": "Swagger",
    "serviceVersion": os.environ["SERVICE_VERSION"],
    "fetchedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "documents": {},
}

for name in os.environ["DOCUMENTS"].split():
    path = os.path.join(output_dir, f"{name}.json")
    with open(path, encoding="utf-8") as file:
        doc = json.load(file)

    version = str(doc.get("openapi", ""))
    paths = doc.get("paths") or {}
    schemas = (doc.get("components") or {}).get("schemas") or {}

    if not version.startswith("3."):
        sys.exit(f"{path}: openapi version {version!r} does not start with '3.'")
    if not paths:
        sys.exit(f"{path}: paths is empty")

    title = (doc.get("info") or {}).get("title", "")
    manifest["documents"][name] = {
        "file": f"{name}.json",
        "openapi": version,
        "title": title,
        "pathCount": len(paths),
        "schemaCount": len(schemas),
    }
    print(f"{path}: openapi {version}, title {title!r}, paths {len(paths)}, schemas {len(schemas)}")

with open(os.path.join(output_dir, "manifest.json"), "w", encoding="utf-8") as file:
    json.dump(manifest, file, indent=2)
    file.write("\n")
PY

step "Done"
echo "Updated $OUTPUT_DIR/{${DOCUMENTS[*]/%/},manifest}.json"
