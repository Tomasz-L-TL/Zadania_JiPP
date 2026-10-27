$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (Get-Command python -ErrorAction SilentlyContinue) {
  python week10/static_check.py
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
dotnet restore week10/Week10.slnx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week10/Week10.slnx --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test week10/tests/Week10.IntegrationTests/Week10.IntegrationTests.csproj --configuration Release --no-build
exit $LASTEXITCODE
