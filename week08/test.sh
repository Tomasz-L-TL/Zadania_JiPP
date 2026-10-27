#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

if ! command -v dotnet >/dev/null 2>&1; then
  echo 'Brak SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0' >&2
  exit 2
fi

python3 week08/static_check.py
dotnet restore week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj
dotnet build week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj --configuration Release --no-restore
dotnet run --project week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj --configuration Release --no-build
