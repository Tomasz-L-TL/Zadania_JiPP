$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (Get-Command python -ErrorAction SilentlyContinue) {
    python week09/tools/static_check.py
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Wymagane .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
}
dotnet restore week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj -c Release --no-restore --logger 'console;verbosity=normal'
exit $LASTEXITCODE
