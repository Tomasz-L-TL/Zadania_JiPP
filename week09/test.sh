#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 week09/tools/static_check.py
if ! command -v dotnet >/dev/null 2>&1; then
  echo 'Wymagane .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0' >&2
  exit 2
fi
dotnet restore week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj
dotnet test week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj \
  --configuration Release --no-restore --logger 'console;verbosity=normal'
