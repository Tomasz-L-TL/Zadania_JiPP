using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Week05.Api.Data;

var project = FindApiProject();
var sqlitePath = Path.Combine(Path.GetTempPath(), $"week05-tests-{Guid.NewGuid():N}.db");
var address = $"http://127.0.0.1:{FindFreePort()}";
var serverLog = new StringBuilder();
using var process = StartServer(project, address, sqlitePath, serverLog);
using var http = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(5) };
int passed = 0;
int failed = 0;
int annaId = 0;
int brendaId = 0;

try
{
    await WaitUntilReady();

    await Test("01 Health check", async () =>
    {
        using var response = await http.GetAsync("/health");
        Status(response, 200);
    });
    await Test("02 Initially empty collection", async () =>
    {
        using var data = await GetJson("/users");
        Must(data.RootElement.GetProperty("items").GetArrayLength() == 0, "Expected empty users.");
    });
    await Test("03 Empty pagination totalPages=0", async () =>
    {
        using var data = await GetJson("/users");
        Must(data.RootElement.GetProperty("totalPages").GetInt32() == 0, "TotalPages should be zero.");
    });
    await Test("04 POST missing name -> 400", () => InvalidPost(new { email = "a@example.com", age = 24 }, "name"));
    await Test("05 POST invalid email -> 400", () => InvalidPost(new { name = "Anna", email = "invalid", age = 24 }, "email"));
    await Test("06 POST underage -> 400", () => InvalidPost(new { name = "Anna", email = "a@example.com", age = 17 }, "age"));
    await Test("07 POST empty body fields -> 400 with 3 errors", () => InvalidPost(new { }, "name", "email", "age"));
    await Test("08 Validation uses problem+json", async () =>
    {
        using var response = await Post(new { name = "", email = "bad", age = 3 });
        Status(response, 400);
        Must(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Wrong media type.");
    });

    await Test("09 POST valid creates resource with 201", async () =>
    {
        using var response = await Post(User("Anna", "anna@example.com", 25));
        Status(response, 201);
        using var json = await ReadJson(response);
        annaId = json.RootElement.GetProperty("id").GetInt32();
        Must(annaId == 1, "First ID should be 1.");
        Must(json.RootElement.GetProperty("name").GetString() == "Anna", "Wrong name.");
    });
    await Test("10 CreatedAtRoute returns valid Location header", async () =>
    {
        using var response = await Post(User("Brenda", "brenda@example.com", 40));
        Status(response, 201);
        using var json = await ReadJson(response);
        brendaId = json.RootElement.GetProperty("id").GetInt32();
        Must(brendaId == 2, "Second ID should be 2.");
        var location = response.Headers.Location;
        Must(location is not null && location.ToString().EndsWith($"/users/{brendaId}", StringComparison.Ordinal),
            "Invalid resource Location header.");
    });
    await Test("11 DTO response excludes internal fields", async () =>
    {
        using var json = await GetJson($"/users/{brendaId}");
        ExpectKeys(json.RootElement, "id", "name", "email", "age", "createdAtUtc", "updatedAtUtc");
    });
    await Test("12 Overposting ID and IsAdmin cannot affect stored user", async () =>
    {
        using var response = await Post(new { id = 999, name = "Bob", email = "bob@example.com", age = 50, isAdmin = true });
        Status(response, 201);
        using var json = await ReadJson(response);
        Must(json.RootElement.GetProperty("id").GetInt32() == 3, "Client set server-generated ID.");
        Must(!json.RootElement.TryGetProperty("isAdmin", out _), "Internal flag was exposed.");
    });
    await Test("13 Duplicate email ignoring case -> 409", async () =>
    {
        using var response = await Post(User("Copy", "ANNA@example.com", 25));
        Status(response, 409);
    });
    await Test("14 Duplicate error contains documented code", async () =>
    {
        using var response = await Post(User("Copy", "anna@EXAMPLE.com", 25));
        Status(response, 409);
        using var json = await ReadJson(response);
        Must(json.RootElement.GetProperty("code").GetString() == "duplicate_email", "Unexpected error code.");
    });
    await Test("15 Failed create does not insert duplicate", async () =>
    {
        using var json = await GetJson("/users");
        Must(json.RootElement.GetProperty("totalItems").GetInt32() == 3, "Duplicate was saved.");
    });
    await Test("16 GET existing ID returns 200", async () =>
    {
        using var response = await http.GetAsync($"/users/{annaId}");
        Status(response, 200);
    });
    await Test("17 GET unknown ID returns 404", () => StatusOnly("/users/99999", 404));
    await Test("18 GET zero ID returns 400", () => StatusOnly("/users/0", 400));
    await Test("19 GET nonnumeric ID route returns 404", () => StatusOnly("/users/hello", 404));

    await Test("20 Create Cathy and Adam (seed for pagination)", async () =>
    {
        using var cathy = await Post(User("Cathy", "cathy@example.com", 20));
        using var adam = await Post(User("Adam", "adam@example.com", 25));
        Status(cathy, 201);
        Status(adam, 201);
    });
    await Test("21 Default pagination page=1, pageSize=10", async () =>
    {
        using var json = await GetJson("/users");
        var root = json.RootElement;
        Must(root.GetProperty("page").GetInt32() == 1, "page != 1");
        Must(root.GetProperty("pageSize").GetInt32() == 10, "pageSize != 10");
        Must(root.GetProperty("totalItems").GetInt32() == 5, "Total != 5");
        Must(root.GetProperty("totalPages").GetInt32() == 1, "totalPages != 1");
    });
    await Test("22 Pagination computes totalPages=3", async () =>
    {
        using var json = await GetJson("/users?page=1&pageSize=2");
        Must(json.RootElement.GetProperty("items").GetArrayLength() == 2, "Expected 2 rows.");
        Must(json.RootElement.GetProperty("totalPages").GetInt32() == 3, "Expected 3 pages.");
    });
    await Test("23 Pagination page 3 has last row", async () =>
    {
        using var json = await GetJson("/users?page=3&pageSize=2");
        Must(json.RootElement.GetProperty("items").GetArrayLength() == 1, "Expected 1 row.");
        Must(json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32() == 5, "Wrong offset.");
    });
    await Test("24 Out-of-range page is empty with correct metadata", async () =>
    {
        using var json = await GetJson("/users?page=4&pageSize=2");
        Must(json.RootElement.GetProperty("items").GetArrayLength() == 0, "Expected empty page.");
        Must(json.RootElement.GetProperty("totalPages").GetInt32() == 3, "Wrong totalPages.");
    });
    await Test("25 Search case insensitive on Name", async () =>
    {
        using var json = await GetJson("/users?search=ANNA");
        Must(json.RootElement.GetProperty("totalItems").GetInt32() == 1, "Case insensitive search failed.");
    });
    await Test("26 Search matches Email", async () =>
    {
        using var json = await GetJson("/users?search=BRENDA%40EXAMPLE.COM");
        Must(json.RootElement.GetProperty("totalItems").GetInt32() == 1, "Email filter failed.");
    });
    await Test("27 Filter minAge and maxAge", async () =>
    {
        using var json = await GetJson("/users?minAge=20&maxAge=25");
        Must(json.RootElement.GetProperty("totalItems").GetInt32() == 3, "Expected three age matches.");
    });
    await Test("28 Filters and pagination apply together", async () =>
    {
        using var json = await GetJson("/users?minAge=20&maxAge=25&pageSize=2&page=2");
        Must(json.RootElement.GetProperty("totalPages").GetInt32() == 2, "Expected two pages.");
        Must(json.RootElement.GetProperty("items").GetArrayLength() == 1, "Expected last filtered item.");
    });
    await Test("29 Sort name ascending", async () =>
    {
        using var json = await GetJson("/users?sortBy=name&sortDir=asc");
        Must(string.Join(",", Names(json)) == "Adam,Anna,Bob,Brenda,Cathy", "Name sort ascending failed.");
    });
    await Test("30 Sort name descending", async () =>
    {
        using var json = await GetJson("/users?sortBy=name&sortDir=desc");
        Must(string.Join(",", Names(json)) == "Cathy,Brenda,Bob,Anna,Adam", "Name sort descending failed.");
    });
    await Test("31 Sort by age ascending", async () =>
    {
        using var json = await GetJson("/users?sortBy=age");
        Must(json.RootElement.GetProperty("items")[0].GetProperty("name").GetString() == "Cathy", "Age sort failed.");
    });
    await Test("32 Sort by ID descending", async () =>
    {
        using var json = await GetJson("/users?sortBy=id&sortDir=desc");
        Must(json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32() == 5, "ID sort failed.");
    });
    await Test("33 Invalid sortBy -> 400", () => StatusOnly("/users?sortBy=password", 400));
    await Test("34 Invalid sortDir -> 400", () => StatusOnly("/users?sortDir=reverse", 400));
    await Test("35 Page=0 -> 400", () => StatusOnly("/users?page=0", 400));
    await Test("36 PageSize=101 -> 400", () => StatusOnly("/users?pageSize=101", 400));
    await Test("37 Invalid age range -> 400", () => StatusOnly("/users?minAge=50&maxAge=20", 400));
    await Test("38 Non-numeric page -> 400", () => StatusOnly("/users?page=abc", 400));
    await Test("39 Too-long search -> 400", () => StatusOnly("/users?search=" + new string('x', 101), 400));

    await Test("40 PUT updates all allowed fields", async () =>
    {
        using var response = await http.PutAsJsonAsync($"/users/{annaId}", User("Ania", "ania@example.com", 30));
        Status(response, 200);
        using var json = await ReadJson(response);
        Must(json.RootElement.GetProperty("name").GetString() == "Ania", "PUT name failed.");
        Must(json.RootElement.GetProperty("age").GetInt32() == 30, "PUT age failed.");
    });
    await Test("41 GET returns updated persisted values", async () =>
    {
        using var json = await GetJson($"/users/{annaId}");
        Must(json.RootElement.GetProperty("email").GetString() == "ania@example.com", "PUT email was not saved.");
    });
    await Test("42 PUT invalid model -> 400", async () =>
    {
        using var response = await http.PutAsJsonAsync($"/users/{annaId}", User("", "bad", 12));
        Status(response, 400);
    });
    await Test("43 PUT duplicate email -> 409", async () =>
    {
        using var response = await http.PutAsJsonAsync($"/users/{annaId}", User("Ania", "brenda@example.com", 30));
        Status(response, 409);
    });
    await Test("44 PUT unknown resource -> 404", async () =>
    {
        using var response = await http.PutAsJsonAsync("/users/99999", User("Other", "new@example.com", 30));
        Status(response, 404);
    });
    await Test("45 PATCH changes only requested age", async () =>
    {
        using var response = await http.PatchAsJsonAsync($"/users/{annaId}", new { age = 31 });
        Status(response, 200);
        using var json = await ReadJson(response);
        Must(json.RootElement.GetProperty("age").GetInt32() == 31, "PATCH age failed.");
        Must(json.RootElement.GetProperty("name").GetString() == "Ania", "PATCH changed another field.");
    });
    await Test("46 Empty PATCH -> 400", async () =>
    {
        using var response = await http.PatchAsJsonAsync($"/users/{annaId}", new { });
        Status(response, 400);
    });
    await Test("47 PATCH invalid email -> 400", async () =>
    {
        using var response = await http.PatchAsJsonAsync($"/users/{annaId}", new { email = "invalid" });
        Status(response, 400);
    });
    await Test("48 PATCH duplicate email -> 409", async () =>
    {
        using var response = await http.PatchAsJsonAsync($"/users/{annaId}", new { email = "BRENDA@example.com" });
        Status(response, 409);
    });
    await Test("49 PATCH unknown resource -> 404", async () =>
    {
        using var response = await http.PatchAsJsonAsync("/users/99999", new { age = 30 });
        Status(response, 404);
    });
    await Test("50 DELETE unknown resource -> 404", async () =>
    {
        using var response = await http.DeleteAsync("/users/99999");
        Status(response, 404);
    });
    await Test("51 DELETE existing resource -> 204, no body", async () =>
    {
        using var response = await http.DeleteAsync($"/users/{annaId}");
        Status(response, 204);
        Must((await response.Content.ReadAsStringAsync()).Length == 0, "204 must have empty body.");
    });
    await Test("52 GET deleted resource -> 404", () => StatusOnly($"/users/{annaId}", 404));
    await Test("53 DELETE again -> 404", () => StatusOnlyDelete($"/users/{annaId}", 404));
    await Test("54 List total after deletion", async () =>
    {
        using var json = await GetJson("/users");
        Must(json.RootElement.GetProperty("totalItems").GetInt32() == 4, "Deleted user remains in list.");
    });
    await Test("55 SQLite migration is applied", async () =>
    {
        await using var db = OpenDirectDatabase();
        var migrations = await db.Database.GetAppliedMigrationsAsync();
        Must(migrations.Contains("202610260001_InitialCreate"), "InitialCreate missing from SQLite migration history.");
    });
    await Test("56 SQLite persisted four users", async () =>
    {
        await using var db = OpenDirectDatabase();
        Must(await db.Users.AsNoTracking().CountAsync() == 4, "SQLite persistence mismatch.");
    });
    await Test("57 Overposting did not create an administrator", async () =>
    {
        await using var db = OpenDirectDatabase();
        Must(!await db.Users.AsNoTracking().AnyAsync(user => user.IsAdmin), "IsAdmin overposting succeeded.");
        Must(!await db.Users.AsNoTracking().AnyAsync(user => user.Id == 999), "ID overposting succeeded.");
    });
    await Test("58 SQLite unique normalized email index", async () =>
    {
        await using var db = OpenDirectDatabase();
        var schema = db.Model.FindEntityType(typeof(Week05.Api.Domain.User));
        Must(schema is not null && schema.GetIndexes().Any(i => i.IsUnique &&
            i.Properties.Any(p => p.Name == "NormalizedEmail")), "Unique email index missing.");
    });
    await Test("59 UpdatedAt is not earlier than CreatedAt", async () =>
    {
        await using var db = OpenDirectDatabase();
        var users = await db.Users.AsNoTracking().ToArrayAsync();
        Must(users.All(user => user.CreatedAtUtc <= user.UpdatedAtUtc), "Invalid timestamps.");
    });
    await Test("60 GET collection does not expose EF entity fields", async () =>
    {
        using var json = await GetJson("/users");
        foreach (var user in json.RootElement.GetProperty("items").EnumerateArray())
            ExpectKeys(user, "id", "name", "email", "age", "createdAtUtc", "updatedAtUtc");
    });
}
catch (Exception ex)
{
    failed++;
    Console.Error.WriteLine("FATAL: " + ex);
    lock (serverLog) Console.Error.WriteLine("API logs:\n" + serverLog);
}
finally
{
    if (!process.HasExited)
    {
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }
    foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        try { File.Delete(sqlitePath + suffix); } catch (IOException) { }
}

Console.WriteLine($"Week05 HTTP + SQLite integration tests: {passed} passed; {failed} failed.");
return passed == 60 && failed == 0 ? 0 : 1;

async Task Test(string title, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"PASS {title}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {title}: {ex.Message}"); }
}

static void Must(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Status(HttpResponseMessage response, int expected)
    => Must((int)response.StatusCode == expected,
        $"Expected HTTP {expected}, got {(int)response.StatusCode}.");

static object User(string name, string email, int age) => new { name, email, age };
Task<HttpResponseMessage> Post(object body) => http.PostAsJsonAsync("/users", body);

async Task<JsonDocument> GetJson(string path)
{
    using var response = await http.GetAsync(path);
    Status(response, 200);
    return await ReadJson(response);
}

static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

static void ExpectKeys(JsonElement element, params string[] keys)
{
    var actual = element.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
    var expected = keys.Order(StringComparer.Ordinal).ToArray();
    Must(actual.SequenceEqual(expected, StringComparer.Ordinal),
        $"Unexpected JSON keys: {string.Join(',', actual)}");
}

static IEnumerable<string?> Names(JsonDocument document)
    => document.RootElement.GetProperty("items").EnumerateArray()
        .Select(x => x.GetProperty("name").GetString());

async Task InvalidPost(object data, params string[] fields)
{
    using var response = await Post(data);
    Status(response, 400);
    using var json = await ReadJson(response);
    var errors = json.RootElement.GetProperty("errors");
    foreach (var field in fields)
        Must(errors.TryGetProperty(field, out var value) && value.GetArrayLength() > 0,
            "Missing validation error for " + field);
}

async Task StatusOnly(string path, int code)
{
    using var response = await http.GetAsync(path);
    Status(response, code);
}
async Task StatusOnlyDelete(string path, int code)
{
    using var response = await http.DeleteAsync(path);
    Status(response, code);
}

UsersDbContext OpenDirectDatabase()
{
    var options = new DbContextOptionsBuilder<UsersDbContext>()
        .UseSqlite($"Data Source={sqlitePath}").Options;
    return new UsersDbContext(options);
}

async Task WaitUntilReady()
{
    for (var i = 0; i < 200; i++)
    {
        if (process.HasExited) throw new InvalidOperationException("API exited unexpectedly.");
        try
        {
            using var response = await http.GetAsync("/health");
            if (response.IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(150);
    }
    throw new TimeoutException("API failed to start. Logs:\n" + serverLog);
}

static string FindApiProject()
{
    var cursor = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (cursor is not null)
    {
        var file = Path.Combine(cursor.FullName, "week05", "src", "Week05.Api", "Week05.Api.csproj");
        if (File.Exists(file)) return file;
        cursor = cursor.Parent;
    }
    throw new FileNotFoundException("Run the tests from the extracted repository root or a subdirectory.");
}

static int FindFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static Process StartServer(string project, string address, string dbPath, StringBuilder logs)
{
    var info = new ProcessStartInfo("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(project)!
    };
    foreach (var arg in new[]
    {
        "run", "--project", project, "-c", "Release", "--no-build", "--no-launch-profile",
        "--", $"--urls={address}"
    }) info.ArgumentList.Add(arg);
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    info.Environment["ConnectionStrings__UsersDb"] = $"Data Source={dbPath}";
    info.Environment["Database__ApplyMigrationsOnStartup"] = "true";
    var process = new Process { StartInfo = info };
    process.OutputDataReceived += (_, args) =>
    {
        if (args.Data is not null) lock (logs) logs.AppendLine(args.Data);
    };
    process.ErrorDataReceived += (_, args) =>
    {
        if (args.Data is not null) lock (logs) logs.AppendLine(args.Data);
    };
    Must(process.Start(), "dotnet run could not start.");
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}
