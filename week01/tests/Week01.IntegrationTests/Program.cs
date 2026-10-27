using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var apiProjectPath = FindApiProject();
var port = GetUnusedLoopbackPort();
var address = $"http://127.0.0.1:{port}";
var serverOutput = new StringBuilder();

using var apiProcess = LaunchApi(apiProjectPath, address, serverOutput);
using var client = new HttpClient
{
    BaseAddress = new Uri(address),
    Timeout = TimeSpan.FromSeconds(4)
};

var passed = 0;
var failed = 0;
var createdId = Guid.Empty;

try
{
    await WaitForServerAsync(client, apiProcess, serverOutput);

    await CheckAsync("01 /hello returns HTTP 200 and JSON", async () =>
    {
        using var response = await client.GetAsync("/hello");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        using var data = await ReadJsonAsync(response);
        Require(!string.IsNullOrWhiteSpace(data.RootElement.GetProperty("message").GetString()),
            "Message must not be empty.");
        Require(response.Content.Headers.ContentType?.MediaType == "application/json",
            "Expected application/json.");
    });

    await CheckAsync("02 /api/v1/hello uses the versioned group", async () =>
    {
        using var response = await client.GetAsync("/api/v1/hello");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
    });

    await CheckAsync("03 /health includes UTC time and app version", async () =>
    {
        using var response = await client.GetAsync("/health");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        using var data = await ReadJsonAsync(response);
        var json = data.RootElement;
        Require(json.GetProperty("status").GetString() == "healthy", "Wrong status.");
        var timestamp = json.GetProperty("timestampUtc").GetDateTimeOffset();
        Require(timestamp.Offset == TimeSpan.Zero, "Timestamp must be UTC.");
        Require(Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalMinutes) < 1,
            "Timestamp is not current.");
        Require(!string.IsNullOrWhiteSpace(json.GetProperty("version").GetString()),
            "Version must be present.");
    });

    await CheckAsync("04 /api/v1/health is available", async () =>
    {
        using var response = await client.GetAsync("/api/v1/health");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
    });

    await CheckAsync("05 /api/v1/config uses DTO and hides secret settings", async () =>
    {
        using var response = await client.GetAsync("/api/v1/config");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        var body = await response.Content.ReadAsStringAsync();
        Require(!body.Contains("DO_NOT_EXPOSE_THIS_VALUE", StringComparison.Ordinal),
            "A secret environment setting leaked into the response.");
        using var data = JsonDocument.Parse(body);
        var json = data.RootElement;
        Require(json.GetProperty("displayName").GetString() == "WWSI Semester 5 - Week 01",
            "App settings were not loaded from appsettings.json.");
        Require(json.GetProperty("supportEmail").GetString() == "help@example.com",
            "Missing public support email.");
        Require(json.GetProperty("maintenanceMode").ValueKind == JsonValueKind.False,
            "Wrong maintenance flag.");
        Require(json.EnumerateObject().Count() == 3,
            "Configuration DTO should expose exactly three fields.");
    });

    await CheckAsync("06 GET /notes returns an empty collection", async () =>
    {
        using var response = await client.GetAsync("/api/v1/notes");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        using var data = await ReadJsonAsync(response);
        Require(data.RootElement.ValueKind == JsonValueKind.Array, "Expected JSON array.");
        Require(data.RootElement.GetArrayLength() == 0, "Fresh memory store must be empty.");
    });

    await CheckAsync("07 POST /notes validates an empty title", async () =>
    {
        using var response = await client.PostAsJsonAsync("/api/v1/notes",
            new { title = "   ", content = "sample" });
        Require(response.StatusCode == HttpStatusCode.BadRequest, "Expected HTTP 400.");
        using var data = await ReadJsonAsync(response);
        Require(data.RootElement.GetProperty("errors").TryGetProperty("title", out _),
            "Validation error for title is missing.");
    });

    await CheckAsync("08 POST /notes validates title max length", async () =>
    {
        using var response = await client.PostAsJsonAsync("/api/v1/notes",
            new { title = new string('X', 101), content = "" });
        Require(response.StatusCode == HttpStatusCode.BadRequest, "Expected HTTP 400.");
    });

    await CheckAsync("09 POST /notes validates content max length", async () =>
    {
        using var response = await client.PostAsJsonAsync("/api/v1/notes",
            new { title = "Valid", content = new string('Y', 2001) });
        Require(response.StatusCode == HttpStatusCode.BadRequest, "Expected HTTP 400.");
    });

    await CheckAsync("10 POST /notes rejects malformed JSON", async () =>
    {
        using var content = new StringContent("{broken json", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/notes", content);
        Require(response.StatusCode == HttpStatusCode.BadRequest, "Expected HTTP 400.");
    });

    await CheckAsync("11 POST /notes creates a resource and returns Location", async () =>
    {
        using var response = await client.PostAsJsonAsync("/api/v1/notes",
            new { title = "  First note  ", content = "  Body  " });
        Require(response.StatusCode == HttpStatusCode.Created, "Expected HTTP 201.");
        using var data = await ReadJsonAsync(response);
        var json = data.RootElement;
        createdId = json.GetProperty("id").GetGuid();
        Require(createdId != Guid.Empty, "Note ID must be a real GUID.");
        Require(json.GetProperty("title").GetString() == "First note", "Title should be trimmed.");
        Require(json.GetProperty("content").GetString() == "Body", "Content should be trimmed.");
        Require(json.GetProperty("createdAtUtc").GetDateTimeOffset().Offset == TimeSpan.Zero,
            "Created time must be UTC.");
        Require(response.Headers.Location?.ToString() == $"/api/v1/notes/{createdId}",
            "Location header must point to the new resource.");
    });

    await CheckAsync("12 GET /notes/{id} returns the created note", async () =>
    {
        using var response = await client.GetAsync($"/api/v1/notes/{createdId}");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        using var data = await ReadJsonAsync(response);
        Require(data.RootElement.GetProperty("id").GetGuid() == createdId,
            "Returned note ID differs from the created ID.");
    });

    await CheckAsync("13 GET /notes lists the new note", async () =>
    {
        using var response = await client.GetAsync("/api/v1/notes");
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        using var data = await ReadJsonAsync(response);
        Require(data.RootElement.GetArrayLength() == 1, "Expected one note.");
        Require(data.RootElement[0].GetProperty("id").GetGuid() == createdId,
            "List contains the wrong note.");
    });

    await CheckAsync("14 GET /notes/{unknownId} returns HTTP 404", async () =>
    {
        using var response = await client.GetAsync($"/api/v1/notes/{Guid.Empty}");
        Require(response.StatusCode == HttpStatusCode.NotFound, "Expected HTTP 404.");
    });

    await CheckAsync("15 DELETE /notes/{id} returns HTTP 204", async () =>
    {
        using var response = await client.DeleteAsync($"/api/v1/notes/{createdId}");
        Require(response.StatusCode == HttpStatusCode.NoContent, "Expected HTTP 204.");
        Require((await response.Content.ReadAsStringAsync()).Length == 0,
            "HTTP 204 must have no response body.");
    });

    await CheckAsync("16 GET deleted note returns HTTP 404", async () =>
    {
        using var response = await client.GetAsync($"/api/v1/notes/{createdId}");
        Require(response.StatusCode == HttpStatusCode.NotFound, "Expected HTTP 404.");
    });

    await CheckAsync("17 DELETE unknown note returns HTTP 404", async () =>
    {
        using var response = await client.DeleteAsync($"/api/v1/notes/{createdId}");
        Require(response.StatusCode == HttpStatusCode.NotFound, "Expected HTTP 404.");
    });

    await CheckAsync("18 GET /notes is empty after deletion", async () =>
    {
        using var response = await client.GetAsync("/api/v1/notes");
        using var data = await ReadJsonAsync(response);
        Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200.");
        Require(data.RootElement.GetArrayLength() == 0, "Note was not removed.");
    });
}
finally
{
    if (!apiProcess.HasExited)
    {
        apiProcess.Kill(entireProcessTree: true);
        await apiProcess.WaitForExitAsync();
    }
}

Console.WriteLine();
Console.WriteLine($"HTTP integration tests: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

async Task CheckAsync(string name, Func<Task> test)
{
    try
    {
        await test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
    JsonDocument.Parse(await response.Content.ReadAsStringAsync());

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string FindApiProject()
{
    for (var folder = new DirectoryInfo(AppContext.BaseDirectory);
         folder is not null;
         folder = folder.Parent)
    {
        var path = Path.Combine(folder.FullName, "src", "Week01.Api", "Week01.Api.csproj");
        if (File.Exists(path))
            return path;
    }

    throw new FileNotFoundException("Cannot locate week01/src/Week01.Api/Week01.Api.csproj.");
}

static int GetUnusedLoopbackPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static Process LaunchApi(string projectPath, string address, StringBuilder output)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = Path.GetDirectoryName(projectPath)!,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("--project");
    startInfo.ArgumentList.Add(projectPath);
    startInfo.ArgumentList.Add("--no-build");
    startInfo.ArgumentList.Add("--no-launch-profile");
    // Match the configuration (Debug/Release) that built the test runner.
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
    startInfo.ArgumentList.Add("--configuration");
    startInfo.ArgumentList.Add(configuration);
    startInfo.Environment["ASPNETCORE_URLS"] = address;
    startInfo.Environment["App__PrivateKey"] = "DO_NOT_EXPOSE_THIS_VALUE";

    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, e) => AppendOutput(e.Data);
    process.ErrorDataReceived += (_, e) => AppendOutput(e.Data);
    if (!process.Start())
        throw new InvalidOperationException("Unable to launch API process.");
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;

    void AppendOutput(string? line)
    {
        if (line is null)
            return;
        lock (output)
        {
            if (output.Length < 32_000)
                output.AppendLine(line);
        }
    }
}

static async Task WaitForServerAsync(HttpClient client, Process server, StringBuilder log)
{
    for (var attempt = 0; attempt < 150; attempt++)
    {
        if (server.HasExited)
            throw new InvalidOperationException($"API exited unexpectedly.\n{log}");

        try
        {
            using var response = await client.GetAsync("/api/v1/health");
            if (response.IsSuccessStatusCode)
                return;
        }
        catch (HttpRequestException)
        {
            // The server has not bound to its port yet.
        }
        catch (TaskCanceledException)
        {
            // The server may still be starting.
        }

        await Task.Delay(200);
    }

    throw new TimeoutException($"API startup timed out.\n{log}");
}
