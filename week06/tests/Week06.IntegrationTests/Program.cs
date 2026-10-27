using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Week06.Api.Data;
using Week06.Api.Domain;
using Week06.Api.Queries;
using Week06.Api.Seeding;

var testsPassed = 0;
var testsFailed = 0;
var databaseFile = Path.Combine(Path.GetTempPath(), $"wwsi-week06-http-{Guid.NewGuid():N}.db");
var port = FreePort();
var baseUrl = $"http://127.0.0.1:{port}";
var logs = new System.Text.StringBuilder();
using var api = StartApi(FindProject(), baseUrl, databaseFile, logs);
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(6) };

int firstTaskId = 0;
int docsTagId = 0;
int backendTagId = 0;

try
{
    await Ready(http, api, logs);

    await Run("01 GET /health -> 200", async () =>
    {
        using var response = await http.GetAsync("/health");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        Require(doc.RootElement.GetProperty("status").GetString() == "healthy", "Health body");
    });
    await Run("02 GET /api/users -> two seeded users", async () =>
    {
        using var response = await http.GetAsync("/api/users");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        Require(doc.RootElement.GetArrayLength() == 2, "Expected 2 users");
        Require(doc.RootElement[0].GetProperty("name").GetString() == "Anna Kowalska", "Wrong first user");
    });
    await Run("03 GET /api/tags -> three seeded tags", async () =>
    {
        using var response = await http.GetAsync("/api/tags");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        Require(doc.RootElement.GetArrayLength() == 3, "Expected 3 tags");
        foreach (var tag in doc.RootElement.EnumerateArray())
        {
            var name = tag.GetProperty("name").GetString();
            if (name == "documentation") docsTagId = tag.GetProperty("id").GetInt32();
            if (name == "backend") backendTagId = tag.GetProperty("id").GetInt32();
        }
        Require(docsTagId > 0 && backendTagId > 0, "Missing sample tags");
    });
    await Run("04 Include: /users/1/tasks returns user name", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/tasks");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        Require(doc.RootElement.GetProperty("name").GetString() == "Anna Kowalska", "Incorrect user");
    });
    await Run("05 Include: /users/1/tasks returns 2 tasks", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/tasks");
        using var doc = await ParseJson(response);
        var items = doc.RootElement.GetProperty("tasks");
        Require(items.GetArrayLength() == 2, "Expected 2 tasks");
        var task = items.EnumerateArray().Single(t => t.GetProperty("title").GetString() == "Implement relation mapping");
        firstTaskId = task.GetProperty("id").GetInt32();
    });
    await Run("06 Include: tasks DTO excludes navigation cycles", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/tasks");
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks")[0];
        Require(!task.TryGetProperty("user", out _) && !task.TryGetProperty("comments", out _), "DTO leaks graph");
    });
    await Run("07 Include: /users/2/tasks returns one task", async () =>
    {
        using var response = await http.GetAsync("/api/users/2/tasks");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        Require(doc.RootElement.GetProperty("tasks").GetArrayLength() == 1, "Jan's task missing");
    });
    await Run("08 Include: unknown user -> 404", () => StatusOnly("/api/users/999/tasks", 404));
    await Run("09 Include: invalid id -> 400", () => StatusOnly("/api/users/0/tasks", 400));
    await Run("10 Route int constraint -> 404", () => StatusOnly("/api/users/abc/tasks", 404));

    await Run("11 ThenInclude returns 3-level DTO", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/details");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        var tasks = doc.RootElement.GetProperty("tasks");
        Require(tasks.GetArrayLength() == 2, "Tasks not loaded");
        Require(tasks.EnumerateArray().Single(t => t.GetProperty("title").GetString() == "Implement relation mapping")
            .GetProperty("comments").GetArrayLength() == 2, "Nested comments not loaded");
    });
    await Run("12 ThenInclude second task has one comment", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/details");
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("title").GetString() == "Write documentation");
        Require(task.GetProperty("comments").GetArrayLength() == 1, "Unexpected comment count");
    });
    await Run("13 ThenInclude confirms comment text", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/details");
        using var doc = await ParseJson(response);
        var allTexts = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .SelectMany(t => t.GetProperty("comments").EnumerateArray())
            .Select(c => c.GetProperty("text").GetString()).ToHashSet();
        Require(allTexts.Contains("Check foreign keys") && allTexts.Contains("Review migration"), "Wrong comments");
    });
    await Run("14 ThenInclude unknown user -> 404", () => StatusOnly("/api/users/999/details", 404));
    await Run("15 ThenInclude invalid id -> 400", () => StatusOnly("/api/users/-1/details", 400));
    await Run("16 Full graph contains 2 task collections", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/graph");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("title").GetString() == "Implement relation mapping");
        Require(task.GetProperty("comments").GetArrayLength() == 2, "Graph comments");
        Require(task.GetProperty("tags").GetArrayLength() == 2, "Graph tags");
    });
    await Run("17 Full graph returns tag names", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/graph");
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("title").GetString() == "Implement relation mapping");
        var names = task.GetProperty("tags").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();
        Require(names.SetEquals(new[] { "backend", "database" }), "Wrong task tags");
    });
    await Run("18 Full graph user 2 has empty comments", async () =>
    {
        using var response = await http.GetAsync("/api/users/2/graph");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        Require(doc.RootElement.GetProperty("tasks")[0].GetProperty("comments").GetArrayLength() == 0, "Expected empty comments");
    });
    await Run("19 Full graph unknown user -> 404", () => StatusOnly("/api/users/999/graph", 404));
    await Run("20 Summary returns counts instead of full comments", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/summary");
        ExpectStatus(response, 200);
        using var doc = await ParseJson(response);
        var root = doc.RootElement;
        Require(root.GetProperty("taskCount").GetInt32() == 2, "Wrong task count");
        Require(root.GetProperty("completedTaskCount").GetInt32() == 1, "Wrong completed count");
        Require(!root.GetProperty("tasks")[0].TryGetProperty("comments", out _), "Summary includes full comments");
    });
    await Run("21 Summary counts nested comments and tags", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/summary");
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("title").GetString() == "Implement relation mapping");
        Require(task.GetProperty("commentCount").GetInt32() == 2, "Comment count mismatch");
        Require(task.GetProperty("tagCount").GetInt32() == 2, "Tag count mismatch");
    });
    await Run("22 Summary unknown user -> 404", () => StatusOnly("/api/users/999/summary", 404));
    await Run("23 Summary invalid id -> 400", () => StatusOnly("/api/users/-2/summary", 400));
    await Run("24 POST tag: missing task -> 404", () => PostStatus($"/api/tasks/999/tags/{docsTagId}", 404));
    await Run("25 POST tag: missing tag -> 404", () => PostStatus($"/api/tasks/{firstTaskId}/tags/999", 404));
    await Run("26 POST tag: invalid task -> 400", () => PostStatus($"/api/tasks/0/tags/{docsTagId}", 400));
    await Run("27 POST tag: invalid tag -> 400", () => PostStatus($"/api/tasks/{firstTaskId}/tags/0", 400));
    await Run("28 POST tag: assigning existing tag -> 204", () => PostStatus($"/api/tasks/{firstTaskId}/tags/{backendTagId}", 204));
    await Run("29 POST tag: adding new tag -> 204", () => PostStatus($"/api/tasks/{firstTaskId}/tags/{docsTagId}", 204));
    await Run("30 POST tag: repeated assignment -> 204", () => PostStatus($"/api/tasks/{firstTaskId}/tags/{docsTagId}", 204));
    await Run("31 Graph confirms newly added tag", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/graph");
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("id").GetInt32() == firstTaskId);
        var tags = task.GetProperty("tags");
        Require(tags.GetArrayLength() == 3, "Expected exactly 3 distinct tags");
        Require(tags.EnumerateArray().Count(t => t.GetProperty("name").GetString() == "documentation") == 1,
            "Duplicate relation was created");
    });
    await Run("32 Summary reflects new tag count", async () =>
    {
        using var response = await http.GetAsync("/api/users/1/summary");
        using var doc = await ParseJson(response);
        var task = doc.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("id").GetInt32() == firstTaskId);
        Require(task.GetProperty("tagCount").GetInt32() == 3, "Summary is stale");
    });

    await Run("33 Migration is recorded in __EFMigrationsHistory", async () =>
        await InExistingDatabase(async db => Require((await db.Database.GetAppliedMigrationsAsync())
            .Contains("202610260001_InitialRelations"), "Migration not applied")));
    await Run("34 No pending migrations", async () =>
        await InExistingDatabase(async db => Require(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Pending EF migrations")));
    await Run("35 Tasks.UserId is foreign key", async () =>
        await InExistingDatabase(async db =>
        {
            var key = db.Model.FindEntityType(typeof(TaskItem))!.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(User));
            Require(key.Properties.Single().Name == "UserId" && key.IsRequired, "Incorrect User FK");
        }));
    await Run("36 Comments.TaskItemId is foreign key", async () =>
        await InExistingDatabase(async db =>
        {
            var key = db.Model.FindEntityType(typeof(Comment))!.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(TaskItem));
            Require(key.Properties.Single().Name == "TaskItemId" && key.IsRequired, "Incorrect Comment FK");
        }));
    await Run("37 Join table has composite unique PK", async () =>
        await InExistingDatabase(async db =>
        {
            var join = db.Model.GetEntityTypes().Single(t => t.GetTableName() == "TaskTags");
            Require(join.FindPrimaryKey()!.Properties.Select(p => p.Name).SequenceEqual(new[] { "TaskItemId", "TagId" }),
                "Composite PK missing");
        }));
    await Run("38 SQL query for full graph can be generated", async () =>
        await InExistingDatabase(async db =>
        {
            var sql = UserQueries.FullGraph(db, 1).ToQueryString();
            Require(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase), "No full graph SELECT");
            Console.WriteLine("----- Full graph: SQL of first split query -----\n" + sql);
        }));
    await Run("39 SQL query for projection can be generated", async () =>
        await InExistingDatabase(async db =>
        {
            var sql = UserQueries.Summary(db, 1).ToQueryString();
            Require(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase), "No summary SELECT");
            Console.WriteLine("----- Projection: EF-generated SQL -----\n" + sql);
        }));
    await Run("40 AsNoTracking leaves tracking empty on full-graph query", async () =>
        await InExistingDatabase(async db =>
        {
            var result = await UserQueries.FullGraph(db, 1).FirstAsync();
            Require(result.Tasks.Count == 2, "Graph not loaded");
            Require(!db.ChangeTracker.Entries().Any(), "Unexpected tracked entities");
        }));
    await Run("41 Projection does not materialize tracked entities", async () =>
        await InExistingDatabase(async db =>
        {
            var result = await UserQueries.Summary(db, 1).FirstAsync();
            Require(result.TaskCount == 2 && !db.ChangeTracker.Entries().Any(), "Projection tracking/count error");
        }));
    await Run("42 Database TaskTags has no duplicate pairs", async () =>
        await InExistingDatabase(async db =>
        {
            var total = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM TaskTags").SingleAsync();
            var distinct = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM (SELECT DISTINCT TaskItemId, TagId FROM TaskTags)").SingleAsync();
            Require(total == distinct, "Duplicate TaskTag pairs");
        }));
    await Run("43 DB prevents duplicate join rows", async () =>
        await InExistingDatabase(async db =>
        {
            var affected = await db.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO TaskTags (TaskItemId, TagId) VALUES ({0}, {1})", firstTaskId, backendTagId);
            Require(affected == 0, "Unique join key ineffective");
        }));
    await Run("44 DB FK rejects orphan task", async () =>
        await InExistingDatabase(async db =>
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO Tasks (UserId, Title, IsCompleted) VALUES (999999, 'Orphan', 0)");
                throw new Exception("Orphan insert succeeded despite FK");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19) { }
        }));
    await Run("45 DB FK rejects orphan comment", async () =>
        await InExistingDatabase(async db =>
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO Comments (TaskItemId, Text) VALUES (999999, 'Orphan')");
                throw new Exception("Orphan comment insert succeeded despite FK");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19) { }
        }));
    await Run("46 Development seeder can be repeated without duplicating", async () =>
        await InExistingDatabase(async db =>
        {
            await DevelopmentSeeder.SeedAsync(db, new FakeHost("Development"));
            Require(await db.Users.CountAsync() == 2 && await db.Tasks.CountAsync() == 3, "Seed produced duplicates");
        }));
    await Run("47 Seeder is forbidden in Production", async () =>
        await InExistingDatabase(async db =>
        {
            try
            {
                await DevelopmentSeeder.SeedAsync(db, new FakeHost("Production"));
                throw new Exception("Production seed unexpectedly succeeded");
            }
            catch (InvalidOperationException) { }
        }));
    await Run("48 Read model three levels uses required FK and one-to-many navigations", async () =>
        await InExistingDatabase(async db =>
        {
            var user = await UserQueries.WithComments(db, 1).FirstAsync();
            Require(user.Tasks.SelectMany(t => t.Comments).Count() == 3, "Wrong comments count");
        }));
}
catch (Exception ex)
{
    testsFailed++;
    Console.Error.WriteLine("SERVER ERROR: " + ex);
    lock (logs) Console.Error.WriteLine(logs.ToString());
}
finally
{
    if (!api.HasExited)
    {
        api.Kill(entireProcessTree: true);
        await api.WaitForExitAsync();
    }
    foreach (var file in new[] { databaseFile, databaseFile + "-wal", databaseFile + "-shm" })
    {
        try { File.Delete(file); } catch (IOException) { }
    }
}

Console.WriteLine($"\nWeek 06 integration results: {testsPassed} passed / {testsFailed} failed.");
return testsFailed == 0 && testsPassed == 48 ? 0 : 1;

async Task Run(string name, Func<Task> run)
{
    try
    {
        await run();
        testsPassed++;
        Console.WriteLine("PASS " + name);
    }
    catch (Exception ex)
    {
        testsFailed++;
        Console.Error.WriteLine("FAIL " + name + " — " + ex.Message);
    }
}

async Task InExistingDatabase(Func<AppDbContext, Task> run)
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={databaseFile}").Options;
    await using var db = new AppDbContext(options);
    await run(db);
}

async Task StatusOnly(string uri, int expected)
{
    using var response = await http.GetAsync(uri);
    ExpectStatus(response, expected);
}

async Task PostStatus(string uri, int expected)
{
    using var response = await http.PostAsync(uri, null);
    ExpectStatus(response, expected);
}

static void Require(bool predicate, string reason)
{
    if (!predicate) throw new InvalidOperationException(reason);
}

static void ExpectStatus(HttpResponseMessage response, int expected)
{
    Require((int)response.StatusCode == expected,
        $"Expected {expected}, received {(int)response.StatusCode}");
}

static async Task<JsonDocument> ParseJson(HttpResponseMessage response) =>
    JsonDocument.Parse(await response.Content.ReadAsStringAsync());

static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var result = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return result;
}

static string FindProject()
{
    DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "week06", "src", "Week06.Api", "Week06.Api.csproj");
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    throw new FileNotFoundException("Uruchom testy z katalogu repozytorium lub jego podfolderu.");
}

static Process StartApi(string project, string baseUrl, string database, System.Text.StringBuilder logs)
{
    var info = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(project)!
    };
    foreach (var arg in new[] { "run", "--project", project, "-c", "Release", "--no-build",
              "--no-launch-profile", "--", "--urls=" + baseUrl })
        info.ArgumentList.Add(arg);
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    info.Environment["ConnectionStrings__Week06Db"] = "Data Source=" + database;

    var p = new Process { StartInfo = info };
    p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (logs) logs.AppendLine(e.Data); };
    p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (logs) logs.AppendLine(e.Data); };
    if (!p.Start()) throw new InvalidOperationException("Cannot start API");
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    return p;
}

static async Task Ready(HttpClient http, Process api, System.Text.StringBuilder logs)
{
    for (var i = 0; i < 200; i++)
    {
        if (api.HasExited) throw new InvalidOperationException("API exited. " + logs);
        try
        {
            using var response = await http.GetAsync("/health");
            if (response.IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(150);
    }
    throw new TimeoutException("API startup timeout: " + logs);
}

sealed class FakeHost(string environment) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environment;
    public string ApplicationName { get; set; } = "Week06.IntegrationTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
