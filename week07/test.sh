#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 week07/static_check.py
dotnet restore week07/src/Week07.Api/Week07.Api.csproj
dotnet build week07/src/Week07.Api/Week07.Api.csproj -c Release --no-restore
dotnet build week07/tests/Week07.IntegrationTests/Week07.IntegrationTests.csproj -c Release
dotnet run --project week07/tests/Week07.IntegrationTests/Week07.IntegrationTests.csproj -c Release --no-build
