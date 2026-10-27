#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
python3 week10/static_check.py
dotnet restore week10/Week10.slnx
dotnet build week10/Week10.slnx --configuration Release --no-restore
dotnet test week10/tests/Week10.IntegrationTests/Week10.IntegrationTests.csproj --configuration Release --no-build
