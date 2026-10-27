#!/usr/bin/env python3
"""Offline consistency checks. Does NOT compile or execute the .NET application."""
from __future__ import annotations

import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parent
repo = root.parent
api = root / "src" / "Week08.Api"
tests = root / "tests" / "Week08.IntegrationTests"
checks: list[tuple[str, bool]] = []


def check(title: str, result: bool) -> None:
    checks.append((title, bool(result)))


def src(path: Path) -> str:
    return path.read_text(encoding="utf-8") if path.is_file() else ""


def has(path: Path, *fragments: str) -> bool:
    code = src(path)
    return bool(code) and all(fragment in code for fragment in fragments)


app_proj = api / "Week08.Api.csproj"
test_proj = tests / "Week08.IntegrationTests.csproj"
program = api / "Program.cs"
correlation = api / "Middleware" / "CorrelationIdMiddleware.cs"
timing = api / "Middleware" / "RequestTimingMiddleware.cs"
exceptions = api / "Middleware" / "GlobalExceptionMiddleware.cs"
identity = api / "Middleware" / "SafeLogIdentity.cs"
health = api / "Diagnostics" / "WritableStorageHealthCheck.cs"
health_response = api / "Diagnostics" / "HealthResponseWriter.cs"
options = api / "Options" / "DiagnosticsOptions.cs"
endpoints = api / "Endpoints" / "DemoEndpoints.cs"
runner = tests / "Program.cs"
workflow = repo / ".github" / "workflows" / "week08-ci.yml"
necessary = [app_proj, test_proj, program, correlation, timing, exceptions, identity,
             health, health_response, options, endpoints, runner, workflow,
             api / "Properties" / "launchSettings.json", api / "appsettings.json",
             api / "appsettings.Development.json", api / "Week08.Api.http",
             root / "test.sh", root / "test.ps1", root / "README.md",
             root / "TEST_REPORT.md", repo / "README.md", repo / ".gitignore"]
for file in necessary:
    check(f"File: {file.relative_to(repo)}", file.is_file() and file.stat().st_size > 0)

for file in [app_proj, test_proj]:
    try:
        parsed = ET.parse(file).getroot()
    except ET.ParseError:
        check(f"{file.name} valid XML", False)
    else:
        check(f"{file.name} valid XML", parsed.tag == "Project")
        check(f"{file.name} net10.0 target", parsed.findtext(".//TargetFramework") == "net10.0")
        check(f"{file.name} nullable enabled", parsed.findtext(".//Nullable") == "enable")
        check(f"{file.name} implicit usings enabled", parsed.findtext(".//ImplicitUsings") == "enable")
        check(f"{file.name} warnings as errors", parsed.findtext(".//TreatWarningsAsErrors") == "true")

check("API uses Web SDK", 'Sdk="Microsoft.NET.Sdk.Web"' in src(app_proj))
check("Tests are console executable", "<OutputType>Exe</OutputType>" in src(test_proj))
check("Test references API", 'ProjectReference Include="../../src/Week08.Api/Week08.Api.csproj"' in src(test_proj))
check("No third-party package references", not any("<PackageReference" in src(p) for p in [app_proj, test_proj]))

for file in [api / "appsettings.json", api / "appsettings.Development.json", api / "Properties" / "launchSettings.json"]:
    try:
        data = json.loads(src(file))
        check(f"JSON syntax: {file.name}", isinstance(data, dict))
    except (json.JSONDecodeError, ValueError):
        check(f"JSON syntax: {file.name}", False)

check("Basic JSON logging uses built-in ILogger", has(program, "builder.Logging.ClearProviders()", "builder.Logging.AddJsonConsole(", "options.IncludeScopes = true"))
check("Program wires Correlation middleware", "UseMiddleware<CorrelationIdMiddleware>()" in src(program))
check("Program wires Timing middleware", "UseMiddleware<RequestTimingMiddleware>()" in src(program))
check("Program wires GlobalException middleware", "UseMiddleware<GlobalExceptionMiddleware>()" in src(program))
check("Middleware order correct", src(program).index("UseMiddleware<CorrelationIdMiddleware>()") < src(program).index("UseMiddleware<RequestTimingMiddleware>()") < src(program).index("UseMiddleware<GlobalExceptionMiddleware>()"))
check("Correlation middleware is custom", has(correlation, "class CorrelationIdMiddleware", "RequestDelegate next", "InvokeAsync(HttpContext context)"))
check("Correlation uses header", has(correlation, '"X-Correlation-ID"', "context.Request.Headers[HeaderName]", "context.Response.Headers[HeaderName]"))
check("Correlation new ID is generated", "Guid.NewGuid().ToString(\"N\")" in src(correlation))
check("Correlation accepts safely validated input", has(correlation, "received.Count == 1", "IsSafe(received.ToString())", "AllowedId.IsMatch(candidate)"))
check("Correlation uses logging scope", has(correlation, "logger.BeginScope(", '["CorrelationId"] = id'))
check("Correlation is available to next middlewares", has(correlation, "context.Items[ItemKey]", "await next(context)"))
check("Timing uses Stopwatch", has(timing, "Stopwatch.StartNew()", "stopwatch.Stop()"))
check("Timing logs once inside finally", has(timing, "finally", "logger.LogInformation(") and src(timing).count("logger.LogInformation(") == 1)
check("Timing has Method, Path, status and latency", has(timing, "Method={Method}", "Path={Path}", "StatusCode={StatusCode}", "ElapsedMs={ElapsedMs}"))
check("Timing correlates logs", has(timing, "CorrelationId={CorrelationId}", "CorrelationIdMiddleware.Get(context)"))
check("Timing uses route template rather than raw URL", has(timing, "RoutePattern.RawText", '"[unmatched]"'))
check("Timing never reads request query/body/headers", not any(x in src(timing) for x in ("Request.QueryString", "Request.Body", "Request.Headers")))
check("Timing has UserId placeholder", "UserId={UserId}" in src(timing))
check("SafeUserID only for authenticated principal", has(identity, "IsAuthenticated != true", "ClaimTypes.NameIdentifier"))
check("SafeUserID limits to GUID", has(identity, "Guid.TryParse", "Guid.Empty"))
check("Global exception handler catches Exception", has(exceptions, "class GlobalExceptionMiddleware", "catch (Exception error)"))
check("Global error handler returns ProblemDetails", has(exceptions, "Results.Problem(", 'title: "Internal Server Error"'))
check("Global error handler returns HTTP 500", "StatusCodes.Status500InternalServerError" in src(exceptions))
check("Global handler returns correlation and trace", has(exceptions, '["correlationId"]', '["traceId"]'))
check("Global handler logs exception type without object", has(exceptions, "LogError(", "error.GetType().Name") and not any(x in src(exceptions) for x in ["LogError(error", "LogError(ex", "error.Message"]))
check("Global handler clears response then restores correlation", has(exceptions, "context.Response.Clear()", "context.Response.Headers[CorrelationIdMiddleware.HeaderName]"))
check("Health checks are registered", has(program, "AddHealthChecks()", 'AddCheck<WritableStorageHealthCheck>("writable-storage"'))
check("Liveness endpoint exists", 'MapGet("/health/live"' in src(program))
check("Dependency health endpoint exists", 'MapHealthChecks("/health/dependencies"' in src(program))
check("Real dependency probes filesystem", has(health, "Directory.CreateDirectory(directory)", "new FileStream(", "stream.WriteAsync("))
check("Temporary dependency probes auto-delete", "FileOptions.DeleteOnClose" in src(health))
check("Dependency failures report Unhealthy", "HealthCheckResult.Unhealthy(" in src(health))
check("Health reports only names and status", has(health_response, "entry.Key", "entry.Value.Status.ToString()") and not any(x in src(health_response) for x in ("Exception", "Description", "Data")))
check("Diagnostics storage directory configurable", has(options, 'SectionName = "Diagnostics"', "StorageDirectory"))
check("GET trace demo endpoint", 'MapGet("/trace"' in src(endpoints))
check("POST private input demo endpoint", 'MapPost("/submit"' in src(endpoints))
check("Throwing demo endpoint gated to Development", has(endpoints, "IsDevelopment()", 'MapGet("/fail"'))
check("Dangerous diagnostic endpoint disabled by default in Production", "MapGet(\"/fail\"" in src(endpoints) and "IsDevelopment()" in src(endpoints))
check("Demo fails with secret marker to test log redaction", "SENSITIVE_EXCEPTION_DO_NOT_LOG_5412" in src(endpoints))

scenario_numbers = re.findall(r'await Check\("(\d{2}) ', src(runner))
check("Exactly 55 sequential test scenarios", scenario_numbers == [f"{i:02d}" for i in range(1, 56)])
check("Integration suite tests true HTTP client", has(runner, "new HttpClient", "StartAsync()", "Client.GetAsync("))
check("Integration suite runs process", has(runner, "ProcessStartInfo(\"dotnet\")", "_process.Start()"))
check("Integration suite uses real captured JSON logs", has(runner, "JsonDocument.Parse(line)", "State", "LogLevel"))
check("Integration suite tests 500 without stack trace", has(runner, "InternalServerError", "StackTrace"))
check("Integration suite tests request content not logged", has(runner, "SECRET_BODY_DO_NOT_LOG", "SECRET_QUERY_DO_NOT_LOG"))
check("Integration suite exercises blocked storage", has(runner, '"blocked-storage"', "ServiceUnavailable"))
check("Integration suite checks production failure endpoint", has(runner, '"Production"', "NotFound"))
check("Integration suite checks correlation propagation", has(runner, "X-Correlation-ID", "ProblemDetails"))
check("Test runner cleans up processes", has(runner, "Kill(entireProcessTree: true)", "WaitForExitAsync()"))
check("CI tests on .NET 10", has(workflow, "10.0.x", "actions/setup-dotnet@v4"))
check("CI checks offline structure", "python3 week08/static_check.py" in src(workflow))
check("CI builds tests and API", "dotnet build week08/tests/Week08.IntegrationTests" in src(workflow))
check("CI executes integration suite", "dotnet run --project week08/tests/Week08.IntegrationTests" in src(workflow))
check("CI workflow includes pull request and push", has(workflow, "pull_request:", "push:"))
check("PowerShell checks process exit codes", "$LASTEXITCODE -ne 0" in src(root / "test.ps1"))
check("Bash script syntax", subprocess.run(["bash", "-n", str(root / "test.sh")], capture_output=True).returncode == 0)
check("Readme contains diagnostic narrative", has(root / "README.md", "Jak zdiagnozować żądanie", "correlationId", "traceId"))
check("Readme covers safe log content", has(root / "README.md", "query stringa", "UserId", "PII"))
check("Readme has reference to the original assignments", "week08/README.md" in src(root / "README.md"))
check("Documentation does not claim executed HTTP tests", "nie zostały tu wykonane" in src(root / "README.md"))
check("No local compiled files packaged", not any(p.name in ("bin", "obj", "data") for p in root.rglob("*")))

passed = sum(ok for _, ok in checks)
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} {name}")
print(f"\nStatic validation: {passed}/{len(checks)} PASS")
sys.exit(0 if passed == len(checks) else 1)
