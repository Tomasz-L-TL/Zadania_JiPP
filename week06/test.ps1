$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (Get-Command python -ErrorAction SilentlyContinue) {
    python week06/static_check.py
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Wymagane SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0'
}

dotnet restore week06/tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build week06/tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project week06/tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj -c Release --no-build
exit $LASTEXITCODE
