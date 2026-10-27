#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
if ! command -v dotnet >/dev/null 2>&1; then
  echo 'Brak .NET 10 SDK — https://dotnet.microsoft.com/download/dotnet/10.0' >&2
  exit 2
fi
python3 week05/static_check.py
dotnet restore week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj
dotnet build week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj -c Release --no-restore
dotnet run --project week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj -c Release --no-build
