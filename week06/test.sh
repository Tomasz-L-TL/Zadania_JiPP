#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"
cd "$root"

python3 week06/static_check.py
if ! command -v dotnet >/dev/null 2>&1; then
  echo 'Wymagane SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0' >&2
  exit 2
fi

dotnet restore week06/tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj
dotnet build week06/tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj -c Release --no-restore
dotnet run --project week06/tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj -c Release --no-build
