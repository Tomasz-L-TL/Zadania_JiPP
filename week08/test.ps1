$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Brak SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0'
}

if (Get-Command python -ErrorAction SilentlyContinue) {
    python week08/static_check.py
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

dotnet restore week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project week08/tests/Week08.IntegrationTests/Week08.IntegrationTests.csproj --configuration Release --no-build
exit $LASTEXITCODE
