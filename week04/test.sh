#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"
api="$here/src/Week04.Api/Week04.Api.csproj"
tests="$here/tests/Week04.IntegrationTests/Week04.IntegrationTests.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Brak .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0" >&2
  exit 2
fi

python3 "$here/static_check.py"
dotnet restore "$tests"
dotnet build "$tests" -c Release --no-restore
dotnet run --project "$tests" -c Release --no-build

echo 'Uruchomienie API i test smoke (Development)...'
port=5084
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__ProductsDb="Data Source=${TMPDIR:-/tmp}/wwsi-week04-smoke-$$.db"
dotnet run --project "$api" -c Release --no-build -- --urls "http://127.0.0.1:$port" > "${TMPDIR:-/tmp}/wwsi-week04-api-$$.log" 2>&1 &
pid=$!
cleanup() {
  kill "$pid" >/dev/null 2>&1 || true
  wait "$pid" >/dev/null 2>&1 || true
  local db="${TMPDIR:-/tmp}/wwsi-week04-smoke-$$.db"
  rm -f "$db" "$db-wal" "$db-shm" "$db-journal" "${TMPDIR:-/tmp}/wwsi-week04-api-$$.log"
}
trap cleanup EXIT

ready=0
for i in $(seq 1 40); do
  if curl -fsS "http://127.0.0.1:$port/api/products" > "${TMPDIR:-/tmp}/wwsi-week04-result-$$.json"; then
    ready=1
    break
  fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then
  cat "${TMPDIR:-/tmp}/wwsi-week04-api-$$.log" >&2
  exit 1
fi
python3 - "${TMPDIR:-/tmp}/wwsi-week04-result-$$.json" <<'PYTEST'
import json, sys
from pathlib import Path
file=Path(sys.argv[1])
try:
    result=json.loads(file.read_text())
    assert len(result)==3, f"oczekiwano 3 produktów, otrzymano {len(result)}"
    print("[OK] API zwraca 3 demo produkty")
finally:
    file.unlink(missing_ok=True)
PYTEST
