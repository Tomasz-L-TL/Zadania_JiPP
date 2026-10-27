$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (Get-Command python -ErrorAction SilentlyContinue) {
    python week07/static_check.py
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
dotnet restore week07/src/Week07.Api/Week07.Api.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week07/src/Week07.Api/Week07.Api.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week07/tests/Week07.IntegrationTests/Week07.IntegrationTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project week07/tests/Week07.IntegrationTests/Week07.IntegrationTests.csproj -c Release --no-build
exit $LASTEXITCODE
