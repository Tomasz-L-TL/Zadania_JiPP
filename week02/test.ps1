$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet build week02/src/Week02.Api/Week02.Api.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week02/tests/Week02.IntegrationTests/Week02.IntegrationTests.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project week02/tests/Week02.IntegrationTests/Week02.IntegrationTests.csproj --configuration Release --no-build
exit $LASTEXITCODE
