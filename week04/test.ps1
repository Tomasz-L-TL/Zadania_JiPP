$ErrorActionPreference = 'Stop'
$folder = $PSScriptRoot
$tests = Join-Path $folder 'tests/Week04.IntegrationTests/Week04.IntegrationTests.csproj'
$api = Join-Path $folder 'src/Week04.Api/Week04.Api.csproj'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Brak .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
}
if (Get-Command python -ErrorAction SilentlyContinue) {
    python (Join-Path $folder 'static_check.py')
    if ($LASTEXITCODE -ne 0) { throw 'Kontrola statyczna nie powiodła się' }
}
dotnet restore $tests
if ($LASTEXITCODE -ne 0) { throw 'Restore nie powiodło się' }
dotnet build $tests -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build nie powiódł się' }
dotnet run --project $tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Testy EF Core nie powiodły się' }

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$database = Join-Path ([System.IO.Path]::GetTempPath()) ("wwsi-week04-smoke-{0}.db" -f [guid]::NewGuid().ToString('N'))
$env:ConnectionStrings__ProductsDb = "Data Source=$database"
$port = Get-Random -Minimum 5100 -Maximum 5900
$stdout = Join-Path ([System.IO.Path]::GetTempPath()) ("week04-{0}.log" -f [guid]::NewGuid().ToString('N'))
$stderr = $stdout + '.err'
$proc = $null
try {
    $proc = Start-Process dotnet -ArgumentList @('run','--project',('"' + $api + '"'),'-c','Release','--no-build','--','--urls',"http://127.0.0.1:$port") -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $success = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 1
        try {
            $items = @(Invoke-RestMethod -Uri "http://127.0.0.1:$port/api/products" -TimeoutSec 2)
            if ($items.Count -ne 3) { throw "Oczekiwano 3 produktów, zwrócono $($items.Count)" }
            $success = $true
            break
        } catch {
            if ($proc.HasExited) { throw "Serwer zakończył pracę; zajrzyj do $stderr" }
        }
    }
    if (-not $success) { throw 'API nie wystartowało prawidłowo' }
    Write-Host '[OK] API zwraca 3 demo produkty'
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Remove-Item $database, ($database + '-wal'), ($database + '-shm'), $stdout, $stderr -ErrorAction SilentlyContinue
    Remove-Item Env:ConnectionStrings__ProductsDb -ErrorAction SilentlyContinue
}
