$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Brak .NET 10 SDK — https://dotnet.microsoft.com/download/dotnet/10.0'
}
if (Get-Command python -ErrorAction SilentlyContinue) {
    python week05/static_check.py
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
dotnet restore week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project week05/tests/Week05.IntegrationTests/Week05.IntegrationTests.csproj -c Release --no-build
exit $LASTEXITCODE
