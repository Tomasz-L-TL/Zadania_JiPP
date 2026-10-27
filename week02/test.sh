#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build week02/src/Week02.Api/Week02.Api.csproj --configuration Release
dotnet build week02/tests/Week02.IntegrationTests/Week02.IntegrationTests.csproj --configuration Release
dotnet run --project week02/tests/Week02.IntegrationTests/Week02.IntegrationTests.csproj --configuration Release --no-build
