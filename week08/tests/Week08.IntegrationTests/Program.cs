using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Week08.Api.Middleware;

var passed = 0;
var failed = 0;
var apiProject = FindApiProject();
var root = Path.Combine(Path.GetTempPath(), $"wwsi-week08-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
var storage = Path.Combine(root, "storage");

using var api = new ApiProcess(apiProject, storage, "Development");
await api.StartAsync();

try
{
    await Check("01 GET /health/live -> 200", async () =>
    {
        using var response = await api.Client.GetAsync("/health/live");
        MustStatus(response, HttpStatusCode.OK);
        using var json = await Json(response);
        Must(json.RootElement.GetProperty("status").GetString() == "healthy", "Wrong health status.");
    });

    await Check("02 Liveness includes generated correlation ID", async () =>
    {
        using var response = await api.Client.GetAsync("/health/live");
        Must(CorrelationIdMiddleware.IsSafe(Header(response)), "Missing/invalid correlation ID.");
    });

    await Check("03 Dependency health -> 200", async () =>
    {
        using var response = await api.Client.GetAsync("/health/dependencies");
        MustStatus(response, HttpStatusCode.OK);
        using var json = await Json(response);
        Must(json.RootElement.GetProperty("status").GetString() == "Healthy", "Expected Healthy.");
    });

    await Check("04 Dependency named writable-storage", async () =>
    {
        using var response = await api.Client.GetAsync("/health/dependencies");
        using var json = await Json(response);
        var dependency = json.RootElement.GetProperty("dependencies")[0];
        Must(dependency.GetProperty("name").GetString() == "writable-storage", "Missing dependency.");
        Must(dependency.GetProperty("status").GetString() == "Healthy", "Dependency unhealthy.");
    });

    await Check("05 Health check does not leave probe files", async () =>
    {
        using var response = await api.Client.GetAsync("/health/dependencies");
        MustStatus(response, HttpStatusCode.OK);
        Must(Directory.Exists(storage), "Storage directory was not created.");
        Must(!Directory.EnumerateFileSystemEntries(storage).Any(), "Probe file remains on disk.");
    });

    await Check("06 GET /api/demo/trace -> 200", async () =>
    {
        using var response = await api.Client.GetAsync("/api/demo/trace");
        MustStatus(response, HttpStatusCode.OK);
    });

    await Check("07 No supplied ID generates 32-character GUID", async () =>
    {
        using var response = await api.Client.GetAsync("/api/demo/trace");
        var id = Header(response);
        Must(id.Length == 32 && Guid.TryParseExact(id, "N", out _), "Generated ID must be GUID N.");
    });

    await Check("08 Body correlation matches response header", async () =>
    {
        using var response = await api.Client.GetAsync("/api/demo/trace");
        using var json = await Json(response);
        Must(json.RootElement.GetProperty("correlationId").GetString() == Header(response), "Body ID mismatch.");
    });

    await Check("09 Valid client ID preserved", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "classroom-08.abc_123");
        MustStatus(response, HttpStatusCode.OK);
        Must(Header(response) == "classroom-08.abc_123", "Client ID was changed.");
    });

    await Check("10 Valid ID preserved in response JSON", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "matching-123");
        using var json = await Json(response);
        Must(json.RootElement.GetProperty("correlationId").GetString() == "matching-123", "Mismatch.");
    });

    await Check("11 Whitespace ID replaced", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "bad id");
        Must(Header(response) != "bad id", "Unsafe ID accepted.");
        Must(Guid.TryParseExact(Header(response), "N", out _), "Expected replacement GUID.");
    });

    await Check("12 Too-long ID replaced", async () =>
    {
        using var response = await api.Get("/api/demo/trace", new string('a', 65));
        Must(Guid.TryParseExact(Header(response), "N", out _), "Long ID not replaced.");
    });

    await Check("13 PII-like ID replaced", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "alice@example.com");
        Must(Guid.TryParseExact(Header(response), "N", out _), "PII-like ID accepted.");
    });

    await Check("14 Multiple IDs replaced", async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/demo/trace");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", new[] { "first", "second" });
        using var response = await api.Client.SendAsync(request);
        Must(Guid.TryParseExact(Header(response), "N", out _), "Multiple IDs accepted.");
    });

    await Check("15 Different requests get different generated IDs", async () =>
    {
        using var first = await api.Client.GetAsync("/api/demo/trace");
        using var second = await api.Client.GetAsync("/api/demo/trace");
        Must(Header(first) != Header(second), "Correlation ID reused.");
    });

    await Check("16 Correlation header on 404", async () =>
    {
        using var response = await api.Get("/unknown-resource", "not-found-08");
        MustStatus(response, HttpStatusCode.NotFound);
        Must(Header(response) == "not-found-08", "Missing correlation on 404.");
    });

    await Check("17 POST private JSON returns 200", async () =>
    {
        using var response = await api.Client.PostAsJsonAsync("/api/demo/submit",
            new { note = "SECRET_BODY_DO_NOT_LOG" });
        MustStatus(response, HttpStatusCode.OK);
    });

    await Check("18 POST does not echo user input", async () =>
    {
        using var response = await api.Client.PostAsJsonAsync("/api/demo/submit",
            new { note = "SECRET_BODY_DO_NOT_LOG" });
        var body = await response.Content.ReadAsStringAsync();
        Must(body.Contains("accepted", StringComparison.OrdinalIgnoreCase), "No acknowledgment.");
        Must(!body.Contains("SECRET_BODY_DO_NOT_LOG", StringComparison.Ordinal), "User input echoed.");
    });

    await Check("19 POST includes correlation header", async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/demo/submit")
        {
            Content = JsonContent.Create(new { note = "ok" })
        };
        request.Headers.Add("X-Correlation-ID", "post-correlation-08");
        using var response = await api.Client.SendAsync(request);
        Must(Header(response) == "post-correlation-08", "Missing POST correlation.");
    });

    await Check("20 GET /api/demo/fail -> 500 in Development", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-20");
        MustStatus(response, HttpStatusCode.InternalServerError);
    });

    await Check("21 HTTP 500 has problem+json", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-21");
        Must(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Expected ProblemDetails.");
    });

    await Check("22 HTTP 500 ProblemDetails has status and title", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-22");
        using var json = await Json(response);
        Must(json.RootElement.GetProperty("status").GetInt32() == 500, "Wrong status.");
        Must(json.RootElement.GetProperty("title").GetString() == "Internal Server Error", "Wrong title.");
    });

    await Check("23 HTTP 500 correlation matches header", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-23");
        using var json = await Json(response);
        Must(Header(response) == "failure-23", "500 lost correlation.");
        Must(json.RootElement.GetProperty("correlationId").GetString() == "failure-23", "500 ProblemDetails lost correlation.");
    });

    await Check("24 HTTP 500 includes a trace identifier", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-24");
        using var json = await Json(response);
        Must(!string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()), "Missing traceId.");
    });

    await Check("25 HTTP 500 does not leak exception messages", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-25");
        var body = await response.Content.ReadAsStringAsync();
        Must(!body.Contains("SENSITIVE_EXCEPTION_DO_NOT_LOG", StringComparison.Ordinal), "Exception message leaked.");
        Must(!body.Contains("InvalidOperationException", StringComparison.Ordinal), "Exception type leaked.");
    });

    await Check("26 HTTP 500 does not leak stack traces", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "failure-26");
        var body = await response.Content.ReadAsStringAsync();
        Must(!body.Contains(" at ", StringComparison.Ordinal) &&
             !body.Contains("StackTrace", StringComparison.OrdinalIgnoreCase) &&
             !body.Contains("Program.cs", StringComparison.Ordinal), "Possible stack trace leak.");
    });

    await Check("27 Logged request timing contains Method, Path, StatusCode, ElapsedMs", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "timed-27");
        MustStatus(response, HttpStatusCode.OK);
        var state = await api.WaitForEvent("timed-27", "RequestTimingMiddleware");
        Must(state.GetProperty("Method").GetString() == "GET", "Wrong method.");
        Must(state.GetProperty("Path").GetString() == "/api/demo/trace", "Wrong route template.");
        Must(state.GetProperty("StatusCode").GetInt32() == 200, "Wrong status in log.");
        Must(state.GetProperty("ElapsedMs").GetDouble() >= 0, "Negative timing.");
    });

    await Check("28 Exactly one request timing log event per request", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "only-one-28");
        await api.WaitForEvent("only-one-28", "RequestTimingMiddleware");
        Must(api.CountEvents("only-one-28", "RequestTimingMiddleware") == 1, "Repeated timing events.");
    });

    await Check("29 Timing log is structured JSON", async () =>
    {
        using var response = await api.Get("/api/demo/trace", "json-log-29");
        var state = await api.WaitForEvent("json-log-29", "RequestTimingMiddleware");
        Must(state.GetProperty("CorrelationId").GetString() == "json-log-29", "Missing structured correlation.");
        Must(state.TryGetProperty("UserId", out _), "Missing UserId structured field.");
    });

    await Check("30 Timing log includes 500 status", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "timed-fail-30");
        MustStatus(response, HttpStatusCode.InternalServerError);
        var state = await api.WaitForEvent("timed-fail-30", "RequestTimingMiddleware");
        Must(state.GetProperty("StatusCode").GetInt32() == 500, "Incorrect timing 500.");
    });

    await Check("31 Exception event is Error level", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "error-31");
        var log = await api.WaitForLog("error-31", "GlobalExceptionMiddleware");
        Must(log.GetProperty("LogLevel").GetString() == "Error", "Expected Error level.");
    });

    await Check("32 Exception log records sanitized type", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "error-32");
        var state = await api.WaitForEvent("error-32", "GlobalExceptionMiddleware");
        Must(state.GetProperty("ExceptionType").GetString() == "InvalidOperationException", "Missing exception type.");
    });

    await Check("33 Exception message never appears in application JSON logs", async () =>
    {
        using var response = await api.Get("/api/demo/fail", "error-33");
        await api.WaitForEvent("error-33", "GlobalExceptionMiddleware");
        Must(!api.AllLogs().Contains("SENSITIVE_EXCEPTION_DO_NOT_LOG_5412", StringComparison.Ordinal),
            "Exception message leaked to logs.");
    });

    await Check("34 Query secrets never appear in application logs", async () =>
    {
        using var response = await api.Get("/api/demo/trace?password=SECRET_QUERY_DO_NOT_LOG", "privacy-34");
        await api.WaitForEvent("privacy-34", "RequestTimingMiddleware");
        Must(!api.AllLogs().Contains("SECRET_QUERY_DO_NOT_LOG", StringComparison.Ordinal), "Query leaked to logs.");
    });

    await Check("35 Body secrets never appear in application logs", async () =>
    {
        using var response = await api.Client.PostAsJsonAsync("/api/demo/submit",
            new { note = "SECRET_BODY_DO_NOT_LOG" });
        MustStatus(response, HttpStatusCode.OK);
        await api.WaitForPathEvent("/api/demo/submit");
        Must(!api.AllLogs().Contains("SECRET_BODY_DO_NOT_LOG", StringComparison.Ordinal), "Body leaked to logs.");
    });

    await Check("36 Unknown URL is not logged raw", async () =>
    {
        using var response = await api.Get("/private/alice@example.com", "privacy-36");
        MustStatus(response, HttpStatusCode.NotFound);
        var state = await api.WaitForEvent("privacy-36", "RequestTimingMiddleware");
        Must(state.GetProperty("Path").GetString() == "[unmatched]", "Untrusted URL logged.");
    });

    await Check("37 Valid IDs are checked by the reusable validator", () => Sync(() =>
    {
        Must(CorrelationIdMiddleware.IsSafe("a"), "One character valid ID rejected.");
        Must(CorrelationIdMiddleware.IsSafe(new string('a', 64)), "64-character ID rejected.");
        Must(!CorrelationIdMiddleware.IsSafe(new string('a', 65)), "Oversize ID accepted.");
        Must(!CorrelationIdMiddleware.IsSafe("foo bar"), "Whitespace accepted.");
        Must(!CorrelationIdMiddleware.IsSafe("foo@example.com"), "Email-like ID accepted.");
    }));

    await Check("38 Anonymous user has no logged UserId", () => Sync(() =>
    {
        Must(SafeLogIdentity.GetUserId(new ClaimsPrincipal()) is null, "Anonymous identity exposed.");
    }));

    await Check("39 Authenticated opaque GUID user ID may be logged", () => Sync(() =>
    {
        var id = Guid.NewGuid().ToString("D");
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "test");
        Must(SafeLogIdentity.GetUserId(new ClaimsPrincipal(identity)) == id, "GUID user ID lost.");
    }));

    await Check("40 User email is never used as a log UserId", () => Sync(() =>
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            { new Claim(ClaimTypes.NameIdentifier, "alice@example.com") }, "test"));
        Must(SafeLogIdentity.GetUserId(principal) is null, "PII exposed as UserId.");
    }));

    await Check("41 Empty GUID is not a safe user identity", () => Sync(() =>
    {
        var identity = new ClaimsIdentity(new[]
            { new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()) }, "test");
        Must(SafeLogIdentity.GetUserId(new ClaimsPrincipal(identity)) is null, "Empty ID accepted.");
    }));

    await Check("42 Dependency health has correlation on success", async () =>
    {
        using var response = await api.Get("/health/dependencies", "storage-42");
        Must(Header(response) == "storage-42", "Missing health correlation.");
    });

    await Check("43 Dependency health status in structured request log", async () =>
    {
        using var response = await api.Get("/health/dependencies", "storage-log-43");
        var state = await api.WaitForEvent("storage-log-43", "RequestTimingMiddleware");
        Must(state.GetProperty("StatusCode").GetInt32() == 200, "Healthy status missing from log.");
    });

    await Check("44 No untrusted query included in unmatched route logs", async () =>
    {
        using var response = await api.Get("/unknown?secret=SECRET_UNMATCHED_44", "unmatched-44");
        var state = await api.WaitForEvent("unmatched-44", "RequestTimingMiddleware");
        Must(state.GetProperty("Path").GetString() == "[unmatched]", "Bad fallback route.");
        Must(!api.AllLogs().Contains("SECRET_UNMATCHED_44", StringComparison.Ordinal), "Unknown query exposed.");
    });

    await Check("45 POST never logs user-submitted correlation-like field", async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/demo/submit")
        {
            Content = JsonContent.Create(new { note = "SENSITIVE_TEST_FIELD_45" })
        };
        request.Headers.Add("X-Correlation-ID", "privacy-45");
        using var response = await api.Client.SendAsync(request);
        MustStatus(response, HttpStatusCode.OK);
        await api.WaitForEvent("privacy-45", "RequestTimingMiddleware");
        Must(!api.AllLogs().Contains("SENSITIVE_TEST_FIELD_45", StringComparison.Ordinal), "Payload leaked.");
    });

    // Separate server with a real blocked storage dependency, not a hardcoded fake response.
    var blocked = Path.Combine(root, "blocked-storage");
    await File.WriteAllTextAsync(blocked, "This path is a regular file, not a writable directory.");
    using var failingApi = new ApiProcess(apiProject, blocked, "Development");
    await failingApi.StartAsync();

    await Check("46 Unwritable dependency -> HTTP 503", async () =>
    {
        using var response = await failingApi.Client.GetAsync("/health/dependencies");
        MustStatus(response, HttpStatusCode.ServiceUnavailable);
    });

    await Check("47 Unwritable dependency -> Unhealthy", async () =>
    {
        using var response = await failingApi.Client.GetAsync("/health/dependencies");
        using var json = await Json(response);
        Must(json.RootElement.GetProperty("status").GetString() == "Unhealthy", "Expected Unhealthy.");
    });

    await Check("48 Unwritable dependency name is present", async () =>
    {
        using var response = await failingApi.Client.GetAsync("/health/dependencies");
        using var json = await Json(response);
        var dep = json.RootElement.GetProperty("dependencies")[0];
        Must(dep.GetProperty("name").GetString() == "writable-storage", "Missing dependency name.");
        Must(dep.GetProperty("status").GetString() == "Unhealthy", "Missing dependency status.");
    });

    await Check("49 Dependency failure keeps liveness healthy", async () =>
    {
        using var response = await failingApi.Client.GetAsync("/health/live");
        MustStatus(response, HttpStatusCode.OK);
    });

    await Check("50 Dependency error response contains no storage filesystem path", async () =>
    {
        using var response = await failingApi.Client.GetAsync("/health/dependencies");
        var body = await response.Content.ReadAsStringAsync();
        Must(!body.Contains(blocked, StringComparison.Ordinal), "Filesystem path leaked.");
    });

    await Check("51 Unhealthy response preserves correlation ID", async () =>
    {
        using var response = await failingApi.Get("/health/dependencies", "bad-storage-51");
        Must(Header(response) == "bad-storage-51", "Missing correlation on HTTP 503.");
    });

    await Check("52 Storage failures are in the timing log", async () =>
    {
        using var response = await failingApi.Get("/health/dependencies", "bad-storage-52");
        var state = await failingApi.WaitForEvent("bad-storage-52", "RequestTimingMiddleware");
        Must(state.GetProperty("StatusCode").GetInt32() == 503, "Missing dependency 503 in logs.");
    });

    // Production server should not expose synthetic exceptions.
    using var productionApi = new ApiProcess(apiProject, Path.Combine(root, "prod-storage"), "Production");
    await productionApi.StartAsync();

    await Check("53 Production does not expose test failure endpoint", async () =>
    {
        using var response = await productionApi.Client.GetAsync("/api/demo/fail");
        MustStatus(response, HttpStatusCode.NotFound);
    });

    await Check("54 Production still returns correlation on 404", async () =>
    {
        using var response = await productionApi.Get("/api/demo/fail", "production-54");
        Must(Header(response) == "production-54", "Production 404 missing correlation.");
    });

    await Check("55 Production dependency health works", async () =>
    {
        using var response = await productionApi.Client.GetAsync("/health/dependencies");
        MustStatus(response, HttpStatusCode.OK);
    });
}
finally
{
    await api.StopAsync();
    try { Directory.Delete(root, recursive: true); } catch (IOException) { }
}

Console.WriteLine($"Week 08 integration checks: {passed} passed, {failed} failed.");
return failed == 0 && passed == 55 ? 0 : 1;

async Task Check(string name, Func<Task> action)
{
    try
    {
        await action();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

static Task Sync(Action action)
{
    action();
    return Task.CompletedTask;
}

static void Must(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void MustStatus(HttpResponseMessage response, HttpStatusCode code)
{
    Must(response.StatusCode == code, $"Expected {(int)code}, got {(int)response.StatusCode}.");
}

static string Header(HttpResponseMessage response) =>
    response.Headers.TryGetValues("X-Correlation-ID", out var values)
        ? values.Single()
        : throw new InvalidOperationException("X-Correlation-ID is missing.");

static async Task<JsonDocument> Json(HttpResponseMessage response) =>
    JsonDocument.Parse(await response.Content.ReadAsStringAsync());

static string FindApiProject()
{
    DirectoryInfo? current = new(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        var project = Path.Combine(current.FullName, "week08", "src", "Week08.Api", "Week08.Api.csproj");
        if (File.Exists(project)) return project;
        current = current.Parent;
    }
    throw new FileNotFoundException("Please run from the ZIP root (containing the week08 directory).");
}

sealed class ApiProcess : IDisposable
{
    private readonly string _project;
    private readonly string _storage;
    private readonly string _environment;
    private Process? _process;
    private readonly ConcurrentQueue<string> _logLines = new();
    private int _port;

    public HttpClient Client { get; private set; } = null!;

    public ApiProcess(string project, string storage, string environment)
    {
        _project = project;
        _storage = storage;
        _environment = environment;
    }

    public async Task StartAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var address = $"http://127.0.0.1:{_port}";

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_project)!
        };
        foreach (var arg in new[]
                 {
                     "run", "--project", _project, "--configuration", "Release", "--no-build",
                     "--no-launch-profile", "--", $"--urls={address}"
                 }) start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = _environment;
        start.Environment["DOTNET_ENVIRONMENT"] = _environment;
        start.Environment["Diagnostics__StorageDirectory"] = _storage;

        _process = new Process { StartInfo = start };
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) _logLines.Enqueue(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _logLines.Enqueue(e.Data); };
        if (!_process.Start()) throw new InvalidOperationException("Cannot launch API process.");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        Client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(4) };

        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (_process.HasExited)
                throw new InvalidOperationException("API process exited early: " + AllLogs());
            try
            {
                using var response = await Client.GetAsync("/health/live");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(150);
        }
        throw new TimeoutException("API did not start: " + AllLogs());
    }

    public async Task<HttpResponseMessage> Get(string path, string correlation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-ID", correlation);
        return await Client.SendAsync(request);
    }

    public string AllLogs() => string.Join("\n", _logLines.ToArray());

    public int CountEvents(string correlation, string category) =>
        AllStructuredLogs().Count(log => Match(log, correlation, category));

    public async Task<JsonElement> WaitForEvent(string correlation, string category)
    {
        var log = await WaitForLog(correlation, category);
        return log.GetProperty("State");
    }

    public async Task<JsonElement> WaitForLog(string correlation, string category)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var log = AllStructuredLogs().FirstOrDefault(value => Match(value, correlation, category));
            if (log.ValueKind == JsonValueKind.Object) return log;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Structured log missing for {correlation} / {category}. {AllLogs()}");
    }

    public async Task WaitForPathEvent(string path)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (AllStructuredLogs().Any(log =>
                    log.TryGetProperty("State", out var state) &&
                    state.ValueKind == JsonValueKind.Object &&
                    state.TryGetProperty("Path", out var property) &&
                    property.GetString() == path)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Missing structured path event: " + path);
    }

    private static bool Match(JsonElement log, string correlation, string category) =>
        log.TryGetProperty("Category", out var source) &&
        source.GetString()?.EndsWith(category, StringComparison.Ordinal) == true &&
        log.TryGetProperty("State", out var state) &&
        state.ValueKind == JsonValueKind.Object &&
        state.TryGetProperty("CorrelationId", out var id) &&
        id.GetString() == correlation;

    private JsonElement[] AllStructuredLogs()
    {
        var logs = new List<JsonElement>();
        foreach (var line in _logLines.ToArray())
        {
            if (!line.StartsWith('{')) continue;
            try
            {
                using var json = JsonDocument.Parse(line);
                logs.Add(json.RootElement.Clone());
            }
            catch (JsonException) { }
        }
        return logs.ToArray();
    }

    public async Task StopAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
    }

    public void Dispose()
    {
        if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        Client?.Dispose();
    }
}
