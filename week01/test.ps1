$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet build tests/Week01.IntegrationTests/Week01.IntegrationTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project tests/Week01.IntegrationTests/Week01.IntegrationTests.csproj -c Release --no-build
exit $LASTEXITCODE
