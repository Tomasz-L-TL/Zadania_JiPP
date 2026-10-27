#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 week03/static_check.py
dotnet build week03/src/Week03.Api/Week03.Api.csproj -c Release
dotnet build week03/tests/Week03.IntegrationTests/Week03.IntegrationTests.csproj -c Release
dotnet run --project week03/tests/Week03.IntegrationTests/Week03.IntegrationTests.csproj -c Release --no-build
