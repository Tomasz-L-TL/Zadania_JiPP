using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var project = FindApiProject();
var port = AllocatePort();
var address = $"http://127.0.0.1:{port}";
var output = new StringBuilder();
using var server = StartServer(project, address, output);
using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };

var pass = 0;
var fail = 0;

try
{
    await WaitForReady();

    await Check("01 GET /health zwraca 200", async () =>
    {
        using var response = await client.GetAsync("/health");
        Status(response, HttpStatusCode.OK);
        using var doc = await Json(response);
        Require(doc.RootElement.GetProperty("status").GetString() == "healthy", "Status health niepoprawny.");
    });
    await Check("02 Początkowa lista użytkowników pusta", async () =>
        Require(await Count("/api/users") == 0, "Lista użytkowników nie jest pusta."));
    await Check("03 Początkowa lista zadań pusta", async () =>
        Require(await Count("/api/tasks") == 0, "Lista zadań nie jest pusta."));

    using var first = await Post("/api/users", User("student01"));
    await Check("04 Rejestracja prawidłowego użytkownika -> 201", () => Sync(() => Status(first, HttpStatusCode.Created)));
    await Check("05 Location poprawnie wskazuje zasób", () => Sync(() =>
        Require(first.Headers.Location?.ToString() == "/api/users/1", "Błędne Location.")));
    using (var firstJson = await Json(first))
    {
        await Check("06 Mapowanie DTO do zwracanego modelu", () => Sync(() =>
        {
            var obj = firstJson.RootElement;
            Require(obj.GetProperty("id").GetInt32() == 1, "ID != 1");
            Require(obj.GetProperty("login").GetString() == "student01", "Login niezgodny.");
            Require(obj.GetProperty("name").GetString() == "Jan Testowy", "Imię niezgodne.");
            Require(obj.GetProperty("age").GetInt32() == 22, "Wiek niezgodny.");
        }));
        await Check("07 DTO odpowiedzi nie ujawnia pól domenowych", () => Sync(() =>
        {
            Require(!firstJson.RootElement.TryGetProperty("createdAtUtc", out _), "Wyciek createdAtUtc.");
            Require(firstJson.RootElement.EnumerateObject().Count() == 5, "Za dużo pól.");
        }));
    }
    await Check("08 GET /api/users/1 zwraca 200", async () =>
    {
        using var response = await client.GetAsync("/api/users/1");
        Status(response, HttpStatusCode.OK);
        using var json = await Json(response);
        Require(json.RootElement.GetProperty("login").GetString() == "student01", "Nie znaleziono użytkownika.");
    });
    await Check("09 GET /api/users zawiera utworzony rekord", async () =>
        Require(await Count("/api/users") == 1, "Powinien istnieć jeden użytkownik."));

    await Check("10 Taki sam login -> 409", () => ExpectConflict(User("student01")));
    await Check("11 Różna wielkość liter -> 409", () => ExpectConflict(User("STUDENT01")));
    await Check("12 Białe znaki wokół loginu -> 400", () => ExpectInvalidUser(User(" student01 "), "login"));
    await Check("13 409 zawiera ProblemDetails", async () =>
    {
        using var response = await Post("/api/users", User("Student01"));
        Status(response, HttpStatusCode.Conflict);
        Require(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Oczekiwano problem+json.");
        using var doc = await Json(response);
        var root = doc.RootElement;
        Require(root.GetProperty("status").GetInt32() == 409, "status != 409");
        Require(root.GetProperty("title").GetString() is { Length: > 0 }, "Brak title.");
        Require(root.GetProperty("code").GetString() == "duplicate_login", "Brak kodu konfliktu.");
    });
    await Check("14 Konflikt nie tworzy drugiego użytkownika", async () =>
        Require(await Count("/api/users") == 1, "Konflikt utworzył użytkownika."));
    await Check("15 Inny login tworzy drugiego użytkownika", async () =>
    {
        using var response = await Post("/api/users", User("student02"));
        Status(response, HttpStatusCode.Created);
        using var json = await Json(response);
        Require(json.RootElement.GetProperty("id").GetInt32() == 2, "ID drugiego użytkownika != 2");
    });

    await Check("16 Brak loginu -> 400 + pole login", () => ExpectInvalidUser(new { name = "Jan Testowy", email = "jan@example.com", age = 22 }, "login"));
    await Check("17 Za krótki login -> 400", () => ExpectInvalidUser(User("ab"), "login"));
    await Check("18 Niedozwolone znaki loginu -> 400", () => ExpectInvalidUser(User("bad login"), "login"));
    await Check("19 Puste imię -> 400", () => ExpectInvalidUser(new { login = "abc000", name = "", email = "jan@example.com", age = 22 }, "name"));
    await Check("20 Za krótkie imię -> 400", () => ExpectInvalidUser(new { login = "abc001", name = "J", email = "jan@example.com", age = 22 }, "name"));
    await Check("21 Niepoprawny e-mail -> 400", () => ExpectInvalidUser(new { login = "abc002", name = "Jan", email = "nie-mail", age = 22 }, "email"));
    await Check("22 Brak e-maila -> 400", () => ExpectInvalidUser(new { login = "abc003", name = "Jan", age = 22 }, "email"));
    await Check("23 Brak wieku -> 400", () => ExpectInvalidUser(new { login = "abc004", name = "Jan", email = "jan@example.com" }, "age"));
    await Check("24 Wiek poniżej minimum -> 400", () => ExpectInvalidUser(new { login = "abc005", name = "Jan", email = "jan@example.com", age = 17 }, "age"));
    await Check("25 Wiek ponad maksimum -> 400", () => ExpectInvalidUser(new { login = "abc006", name = "Jan", email = "jan@example.com", age = 121 }, "age"));
    await Check("26 Login 31 znaków -> 400", () => ExpectInvalidUser(User(new string('a', 31)), "login"));
    await Check("27 E-mail za długi -> 400", () => ExpectInvalidUser(new { login = "abc007", name = "Jan", email = new string('a', 115) + "@example.com", age = 22 }, "email"));
    await Check("28 Login zawierający spacje -> 400", () => ExpectInvalidUser(User("foo bar"), "login"));
    await Check("29 Null zamiast loginu -> 400", () => ExpectInvalidUser(new { login = (string?)null, name = "Jan", email = "jan@example.com", age = 22 }, "login"));
    await Check("30 Pusty obiekt zwraca 4 błędy pól", async () =>
    {
        using var response = await Post("/api/users", new { });
        await Problem(response, 400, "login", "name", "email", "age");
    });
    await Check("31 Cztery błędne pola zgłaszane jednocześnie", async () =>
    {
        using var response = await Post("/api/users", new { login = "x", name = "J", email = "xyz", age = 0 });
        await Problem(response, 400, "login", "name", "email", "age");
    });
    await Check("32 Walidacja daje komunikaty przypisane do pól", async () =>
    {
        using var response = await Post("/api/users", User("x"));
        using var json = await Json(response);
        Require(json.RootElement.GetProperty("errors").GetProperty("login")[0].GetString() is { Length: > 3 }, "Brak komunikatu login.");
    });
    await Check("33 Złe dane nie zmieniają kolekcji użytkowników", async () =>
        Require(await Count("/api/users") == 2, "Walidacja dodała rekord."));
    await Check("34 Granica: wiek 18 poprawny", async () =>
    {
        using var response = await Post("/api/users", new { login = "adult18", name = "Jan", email = "jan18@example.com", age = 18 });
        Status(response, HttpStatusCode.Created);
    });
    await Check("35 Granica: wiek 120 poprawny", async () =>
    {
        using var response = await Post("/api/users", new { login = "adult120", name = "Jan", email = "jan120@example.com", age = 120 });
        Status(response, HttpStatusCode.Created);
    });
    await Check("36 Granica: login 30 znaków poprawny", async () =>
    {
        using var response = await Post("/api/users", User(new string('k', 30)));
        Status(response, HttpStatusCode.Created);
    });

    using var task1 = await Post("/api/tasks", TaskInput("Dodać walidację", 3));
    await Check("37 POST /api/tasks poprawne -> 201", () => Sync(() => Status(task1, HttpStatusCode.Created)));
    await Check("38 Location poprawne dla zadania", () => Sync(() =>
        Require(task1.Headers.Location?.ToString() == "/api/tasks/1", "Niepoprawne Location zadania.")));
    using (var taskJson = await Json(task1))
    {
        await Check("39 Mapowanie modelu zadania", () => Sync(() =>
        {
            var task = taskJson.RootElement;
            Require(task.GetProperty("id").GetInt32() == 1, "ID != 1");
            Require(task.GetProperty("priority").GetInt32() == 3, "Priorytet niepoprawny.");
            Require(task.GetProperty("title").GetString() == "Dodać walidację", "Tytuł niepoprawny.");
            Require(!task.TryGetProperty("createdAtUtc", out _), "Wyciek CreatedAtUtc.");
        }));
    }
    await Check("40 GET /api/tasks/1 działa", async () =>
    {
        using var response = await client.GetAsync("/api/tasks/1");
        Status(response, HttpStatusCode.OK);
        using var doc = await Json(response);
        Require(doc.RootElement.GetProperty("id").GetInt32() == 1, "Brak zadania.");
    });
    await Check("41 GET /api/tasks zawiera zadanie", async () =>
        Require(await Count("/api/tasks") == 1, "Lista zadań != 1."));
    await Check("42 Brak tytułu zadania -> 400", () => ExpectInvalidTask(new { priority = 2 }, "title"));
    await Check("43 Za krótki tytuł zadania -> 400", () => ExpectInvalidTask(TaskInput("xx", 2), "title"));
    await Check("44 Za długi tytuł zadania -> 400", () => ExpectInvalidTask(Task(new string('t', 121), 2), "title"));
    await Check("45 Brak priorytetu zadania -> 400", () => ExpectInvalidTask(new { title = "Zadanie testowe" }, "priority"));
    await Check("46 Priorytet 0 -> 400", () => ExpectInvalidTask(TaskInput("Zadanie testowe", 0), "priority"));
    await Check("47 Priorytet 6 -> 400", () => ExpectInvalidTask(TaskInput("Zadanie testowe", 6), "priority"));
    await Check("48 Za długi opis zadania -> 400", () => ExpectInvalidTask(new { title = "Zadanie testowe", description = new string('d', 501), priority = 2 }, "description"));
    await Check("49 Wiele błędów w zadaniu -> jeden ValidationProblem", async () =>
    {
        using var response = await Post("/api/tasks", new { title = "x", description = new string('d', 501), priority = 6 });
        await Problem(response, 400, "title", "description", "priority");
    });
    await Check("50 Walidacja nie dodaje niepoprawnego zadania", async () =>
        Require(await Count("/api/tasks") == 1, "Walidacja dodała niepoprawne zadanie."));
    await Check("51 Opis opcjonalny -> 201", async () =>
    {
        using var response = await Post("/api/tasks", new { title = "Bez opisu", priority = 1 });
        Status(response, HttpStatusCode.Created);
        using var doc = await Json(response);
        Require(doc.RootElement.GetProperty("description").ValueKind == JsonValueKind.Null, "Opis powinien być null.");
    });
    await Check("52 Graniczny priorytet 5 -> 201", async () =>
    {
        using var response = await Post("/api/tasks", TaskInput("Wysoki priorytet", 5));
        Status(response, HttpStatusCode.Created);
    });
    await Check("53 Graniczny tytuł 120 znaków -> 201", async () =>
    {
        using var response = await Post("/api/tasks", Task(new string('t', 120), 1));
        Status(response, HttpStatusCode.Created);
    });
    await Check("54 Nieistniejący użytkownik -> 404 + ProblemDetails", () => ExpectNotFound("/api/users/9999"));
    await Check("55 Nieistniejące zadanie -> 404 + ProblemDetails", () => ExpectNotFound("/api/tasks/9999"));
    await Check("56 Niepoprawny JSON nie jest akceptowany", async () =>
    {
        using var request = new StringContent("{ broken", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/users", request);
        Status(response, HttpStatusCode.BadRequest);
    });
    await Check("57 Jednoczesny POST identycznego loginu -> dokładnie 1 x 201", async () =>
    {
        // Sprawdza atomowe wykonanie reguły biznesowej, bez TOCTOU race.
        var concurrent = Enumerable.Range(0, 8).Select(_ => Post("/api/users", User("same-concurrent"))).ToArray();
        var responses = await Task.WhenAll(concurrent);
        try
        {
            Require(responses.Count(r => r.StatusCode == HttpStatusCode.Created) == 1, "Nie dokładnie 1 rejestracja.");
            Require(responses.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 7, "Nie dokładnie 7 konfliktów.");
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    });
    await Check("58 Po współbieżnej rejestracji dokładnie 6 użytkowników", async () =>
        Require(await Count("/api/users") == 6, "Oczekiwano 6 użytkowników."));
    await Check("59 Oba endpointy używają spójnego type w ValidationProblem", async () =>
    {
        using var user = await Post("/api/users", User("x"));
        using var task = await Post("/api/tasks", TaskInput("x", 6));
        using var userDoc = await Json(user);
        using var taskDoc = await Json(task);
        Require(userDoc.RootElement.GetProperty("type").GetString() == taskDoc.RootElement.GetProperty("type").GetString(), "Różne formaty type.");
    });
    await Check("60 Błędy obu DTO zwracają application/problem+json", async () =>
    {
        using var user = await Post("/api/users", User("x"));
        using var task = await Post("/api/tasks", TaskInput("x", 6));
        Require(user.Content.Headers.ContentType?.MediaType == "application/problem+json", "User content type.");
        Require(task.Content.Headers.ContentType?.MediaType == "application/problem+json", "Task content type.");
    });
}
catch (Exception exception)
{
    fail++;
    Console.Error.WriteLine("BŁĄD URUCHOMIENIA: " + exception);
    Console.Error.WriteLine("Log serwera:\n" + output);
}
finally
{
    if (!server.HasExited)
    {
        server.Kill(entireProcessTree: true);
        server.WaitForExit(5000);
    }
}

Console.WriteLine($"\nWEEK 03: {pass} zaliczonych / {pass + fail} wykonanych scenariuszy.");
Environment.ExitCode = fail == 0 && pass == 60 ? 0 : 1;

async Task Check(string name, Func<Task> action)
{
    try
    {
        await action();
        pass++;
        Console.WriteLine($"[OK] {name}");
    }
    catch (Exception exception)
    {
        fail++;
        Console.Error.WriteLine($"[FAIL] {name}: {exception.Message}");
    }
}

static Task Sync(Action action)
{
    action();
    return Task.CompletedTask;
}

static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Status(HttpResponseMessage response, HttpStatusCode expected)
{
    Require(response.StatusCode == expected, $"HTTP {(int)response.StatusCode}, oczekiwano {(int)expected}.");
}

static async Task<JsonDocument> Json(HttpResponseMessage response) =>
    JsonDocument.Parse(await response.Content.ReadAsStringAsync());

static object User(string login) => new { login, name = "Jan Testowy", email = "jan@example.com", age = 22 };
static object TaskInput(string title, int priority) => new { title, description = "Opis", priority };

async Task<HttpResponseMessage> Post(string endpoint, object data) =>
    await client.PostAsJsonAsync(endpoint, data);

async Task<int> Count(string endpoint)
{
    using var response = await client.GetAsync(endpoint);
    Status(response, HttpStatusCode.OK);
    using var doc = await Json(response);
    return doc.RootElement.GetArrayLength();
}

async Task ExpectConflict(object data)
{
    using var response = await Post("/api/users", data);
    Status(response, HttpStatusCode.Conflict);
}

async Task ExpectInvalidUser(object data, params string[] names)
{
    using var response = await Post("/api/users", data);
    await Problem(response, 400, names);
}

async Task ExpectInvalidTask(object data, params string[] names)
{
    using var response = await Post("/api/tasks", data);
    await Problem(response, 400, names);
}

async Task Problem(HttpResponseMessage response, int status, params string[] names)
{
    Require((int)response.StatusCode == status, $"HTTP {(int)response.StatusCode} zamiast {status}.");
    Require(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Nie zwrócono application/problem+json.");
    using var doc = await Json(response);
    var root = doc.RootElement;
    Require(root.GetProperty("status").GetInt32() == status, "Brak prawidłowego status w ProblemDetails.");
    Require(root.GetProperty("title").GetString() is { Length: > 0 }, "Brak title w ProblemDetails.");
    var errors = root.GetProperty("errors");
    foreach (var name in names)
    {
        Require(errors.TryGetProperty(name, out var values), $"Brak errors.{name}.");
        Require(values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0, $"errors.{name} nie zawiera komunikatów.");
    }
}

async Task ExpectNotFound(string endpoint)
{
    using var response = await client.GetAsync(endpoint);
    Status(response, HttpStatusCode.NotFound);
    using var doc = await Json(response);
    Require(doc.RootElement.GetProperty("status").GetInt32() == 404, "Brak 404 ProblemDetails.");
}

async Task WaitForReady()
{
    for (int i = 0; i < 100; i++)
    {
        if (server.HasExited) throw new InvalidOperationException("Serwer zakończył się przed testami. " + output);
        try
        {
            using var response = await client.GetAsync("/health");
            if (response.IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(150);
    }
    throw new TimeoutException("Serwer nie uruchomił się na czas. " + output);
}

static string FindApiProject()
{
    DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        var project = Path.Combine(directory.FullName, "week03", "src", "Week03.Api", "Week03.Api.csproj");
        if (File.Exists(project)) return project;
        directory = directory.Parent;
    }
    throw new FileNotFoundException("Nie znaleziono week03/src/Week03.Api/Week03.Api.csproj.");
}

static int AllocatePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static Process StartServer(string project, string address, StringBuilder output)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    foreach (string arg in new[]
    {
        "run", "--project", project, "--configuration", "Release", "--no-build", "--no-launch-profile",
        "--", $"--urls={address}"
    }) startInfo.ArgumentList.Add(arg);

    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
    process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
    if (!process.Start()) throw new InvalidOperationException("Nie udało się uruchomić serwera.");
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}
