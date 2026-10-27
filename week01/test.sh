#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet build tests/Week01.IntegrationTests/Week01.IntegrationTests.csproj -c Release
dotnet run --project tests/Week01.IntegrationTests/Week01.IntegrationTests.csproj -c Release --no-build
