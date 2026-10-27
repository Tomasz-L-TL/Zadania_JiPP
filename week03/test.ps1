$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
python week03/static_check.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week03/src/Week03.Api/Week03.Api.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week03/tests/Week03.IntegrationTests/Week03.IntegrationTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project week03/tests/Week03.IntegrationTests/Week03.IntegrationTests.csproj -c Release --no-build
exit $LASTEXITCODE
