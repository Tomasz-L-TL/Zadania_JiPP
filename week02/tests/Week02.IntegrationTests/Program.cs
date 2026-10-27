using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

string apiProject = FindApiProject();
int port = FindFreePort();
string address = $"http://127.0.0.1:{port}";
var output = new StringBuilder();
using var api = StartApi(apiProject, address, output);
using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(5) };

int passed = 0;
int failed = 0;

try
{
    await WaitUntilReady(client, api, output);

    // Cwiczenie 1: route constraints, walidacja i 200/400/404.
    await Check("01 GET /users/1 -> 200 i czytelny JSON", async () =>
    {
        using var response = await client.GetAsync("/users/1");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("id").GetInt32() == 1, "ID should be 1.");
        Expect(json.RootElement.GetProperty("name").GetString() == "Anna Kowalska", "User name mismatch.");
    });
    await Check("02 GET /users/0 -> 400", () => ExpectError("/users/0", HttpStatusCode.BadRequest, "invalid_id"));
    await Check("03 GET /users/-2 -> 400", () => ExpectError("/users/-2", HttpStatusCode.BadRequest, "invalid_id"));
    await Check("04 GET /users/999 -> 404", () => ExpectError("/users/999", HttpStatusCode.NotFound, "user_not_found"));
    await Check("05 constraint int: /users/abc -> 404", () => ExpectStatusOnly("/users/abc", HttpStatusCode.NotFound));
    await Check("06 sciezka nadmiarowa -> 404", () => ExpectStatusOnly("/users/1/extra", HttpStatusCode.NotFound));

    // Cwiczenie 2: query binding, wartosci domyslne, filtrowanie, stronicowanie.
    await Check("07 GET /users: domyslne page=1 i pageSize=10", async () =>
    {
        using var response = await client.GetAsync("/users");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        var root = json.RootElement;
        Expect(root.GetProperty("page").GetInt32() == 1, "Default page should be 1.");
        Expect(root.GetProperty("pageSize").GetInt32() == 10, "Default pageSize should be 10.");
        Expect(root.GetProperty("totalItems").GetInt32() == 12, "Seed should contain 12 users.");
        Expect(root.GetProperty("totalPages").GetInt32() == 2, "There should be 2 pages.");
        Expect(root.GetProperty("items").GetArrayLength() == 10, "First page should contain 10 users.");
    });
    await Check("08 page=2 zwraca pozostalych 2 uzytkownikow", async () =>
    {
        using var response = await client.GetAsync("/users?page=2");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("items").GetArrayLength() == 2, "Wrong item count.");
        Expect(json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32() == 11, "Wrong offset.");
    });
    await Check("09 page poza wynikiem -> 200 z pusta lista", async () =>
    {
        using var response = await client.GetAsync("/users?page=3");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("items").GetArrayLength() == 0, "Expected empty page.");
    });
    await Check("10 search=anna -> 2 dopasowania", async () =>
    {
        using var response = await client.GetAsync("/users?search=anna");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("totalItems").GetInt32() == 2, "Anna should match two users.");
    });
    await Check("11 search jest niewrazliwy na wielkosc liter", async () =>
    {
        using var response = await client.GetAsync("/users?search=ANNA");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("totalItems").GetInt32() == 2, "Search should ignore case.");
    });
    await Check("12 search filtruje tez po adresie email", async () =>
    {
        using var response = await client.GetAsync("/users?search=nowak%40example.com");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("totalItems").GetInt32() == 1, "Expected exactly one email match.");
        Expect(json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32() == 2, "Wrong email match.");
    });
    await Check("13 search + page + pageSize razem", async () =>
    {
        using var response = await client.GetAsync("/users?search=anna&page=2&pageSize=1");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        var root = json.RootElement;
        Expect(root.GetProperty("totalItems").GetInt32() == 2, "Wrong totalItems.");
        Expect(root.GetProperty("totalPages").GetInt32() == 2, "Wrong totalPages.");
        Expect(root.GetProperty("items").GetArrayLength() == 1, "Wrong page length.");
        Expect(root.GetProperty("items")[0].GetProperty("id").GetInt32() == 11, "Wrong filtered page.");
    });
    await Check("14 search bez wyniku -> 200 z totalPages=0", async () =>
    {
        using var response = await client.GetAsync("/users?search=notexistingstring");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("totalItems").GetInt32() == 0, "Expected no items.");
        Expect(json.RootElement.GetProperty("totalPages").GetInt32() == 0, "Expected zero pages.");
    });
    await Check("15 puste/odstepowe search nie filtruje", async () =>
    {
        using var response = await client.GetAsync("/users?search=%20%20%20");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("totalItems").GetInt32() == 12, "Expected all users.");
    });
    await Check("16 page=0 -> 400", () => ExpectError("/users?page=0", HttpStatusCode.BadRequest, "invalid_page"));
    await Check("17 page=-1 -> 400", () => ExpectError("/users?page=-1", HttpStatusCode.BadRequest, "invalid_page"));
    await Check("18 page=10001 -> 400", () => ExpectError("/users?page=10001", HttpStatusCode.BadRequest, "invalid_page"));
    await Check("19 pageSize=0 -> 400", () => ExpectError("/users?pageSize=0", HttpStatusCode.BadRequest, "invalid_page_size"));
    await Check("20 pageSize=51 -> 400", () => ExpectError("/users?pageSize=51", HttpStatusCode.BadRequest, "invalid_page_size"));
    await Check("21 page=abc -> 400 (binding)", () => ExpectStatusOnly("/users?page=abc", HttpStatusCode.BadRequest));
    await Check("22 pageSize=abc -> 400 (binding)", () => ExpectStatusOnly("/users?pageSize=abc", HttpStatusCode.BadRequest));
    await Check("23 za dlugi search -> 400", () => ExpectError($"/users?search={new string('x', 101)}", HttpStatusCode.BadRequest, "invalid_search"));
    await Check("24 pageSize=50 jest akceptowane", async () =>
    {
        using var response = await client.GetAsync("/users?pageSize=50");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("items").GetArrayLength() == 12, "Wrong count at limit.");
    });

    // Cwiczenie 3: oddzielne route groups i brak konfliktow tras.
    await Check("25 GET /tasks -> 200, lista zadan", async () =>
    {
        using var response = await client.GetAsync("/tasks");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetArrayLength() == 3, "Wrong tasks count.");
    });
    await Check("26 GET /tasks/1 -> 200, TaskDto", async () =>
    {
        using var response = await client.GetAsync("/tasks/1");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetProperty("id").GetInt32() == 1, "Wrong task ID.");
        Expect(json.RootElement.GetProperty("completed").GetBoolean(), "Task should be completed.");
        Expect(json.RootElement.GetProperty("assigneeId").GetInt32() == 1, "Wrong assignee.");
    });
    await Check("27 GET /tasks/0 -> 400", () => ExpectError("/tasks/0", HttpStatusCode.BadRequest, "invalid_id"));
    await Check("28 GET /tasks/999 -> 404", () => ExpectError("/tasks/999", HttpStatusCode.NotFound, "task_not_found"));
    await Check("29 GET /tasks/nope -> 404", () => ExpectStatusOnly("/tasks/nope", HttpStatusCode.NotFound));
    await Check("30 brak konfliktu /users/tasks -> 404", () => ExpectStatusOnly("/users/tasks", HttpStatusCode.NotFound));

    // Cwiczenie 4/5: typowane rezultaty, kontrakty v1/v2, wspolne zrodlo danych.
    await Check("31 GET /api/v1/products -> 200, stary kontrakt", async () =>
    {
        using var response = await client.GetAsync("/api/v1/products");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetArrayLength() == 4, "Wrong products count.");
        var product = json.RootElement[0];
        ExpectKeys(product, "id", "name", "price", "available");
        Expect(product.GetProperty("price").ValueKind == JsonValueKind.Number, "v1 price must be numeric.");
        Expect(product.GetProperty("available").GetBoolean(), "v1 should indicate availability.");
    });
    await Check("32 GET /api/v2/products -> 200, nowy kontrakt", async () =>
    {
        using var response = await client.GetAsync("/api/v2/products");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        Expect(json.RootElement.GetArrayLength() == 4, "Wrong products count.");
        var product = json.RootElement[0];
        ExpectKeys(product, "id", "name", "price", "category", "inStock");
        ExpectKeys(product.GetProperty("price"), "amount", "currency");
        Expect(product.GetProperty("price").GetProperty("currency").GetString() == "PLN", "Missing currency.");
    });
    await Check("33 v1 i v2 korzystaja z tych samych danych", async () =>
    {
        using var a = await client.GetAsync("/api/v1/products");
        using var b = await client.GetAsync("/api/v2/products");
        ExpectStatus(a, HttpStatusCode.OK);
        ExpectStatus(b, HttpStatusCode.OK);
        using var v1 = await ReadJson(a);
        using var v2 = await ReadJson(b);
        Expect(v1.RootElement.GetArrayLength() == v2.RootElement.GetArrayLength(), "Counts differ.");
        for (int i = 0; i < v1.RootElement.GetArrayLength(); i++)
        {
            var oldProduct = v1.RootElement[i];
            var newProduct = v2.RootElement[i];
            Expect(oldProduct.GetProperty("id").GetInt32() == newProduct.GetProperty("id").GetInt32(), "ID changed.");
            Expect(oldProduct.GetProperty("name").GetString() == newProduct.GetProperty("name").GetString(), "Name changed.");
            Expect(oldProduct.GetProperty("price").GetDecimal() == newProduct.GetProperty("price").GetProperty("amount").GetDecimal(), "Amount changed.");
            Expect(oldProduct.GetProperty("available").GetBoolean() == newProduct.GetProperty("inStock").GetBoolean(), "Availability changed.");
        }
    });
    await Check("34 GET /api/v1/products/1 -> 200", async () =>
    {
        using var response = await client.GetAsync("/api/v1/products/1");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        ExpectKeys(json.RootElement, "id", "name", "price", "available");
        Expect(json.RootElement.GetProperty("price").GetDecimal() == 149.90m, "Wrong v1 price.");
    });
    await Check("35 GET /api/v2/products/1 -> 200", async () =>
    {
        using var response = await client.GetAsync("/api/v2/products/1");
        ExpectStatus(response, HttpStatusCode.OK);
        using var json = await ReadJson(response);
        ExpectKeys(json.RootElement, "id", "name", "price", "category", "inStock");
        Expect(json.RootElement.GetProperty("price").GetProperty("amount").GetDecimal() == 149.90m, "Wrong v2 price.");
    });
    await Check("36 GET /api/v1/products/0 -> 400", () => ExpectError("/api/v1/products/0", HttpStatusCode.BadRequest, "invalid_id"));
    await Check("37 GET /api/v2/products/-1 -> 400", () => ExpectError("/api/v2/products/-1", HttpStatusCode.BadRequest, "invalid_id"));
    await Check("38 GET /api/v1/products/999 -> 404", () => ExpectError("/api/v1/products/999", HttpStatusCode.NotFound, "product_not_found"));
    await Check("39 GET /api/v2/products/999 -> 404", () => ExpectError("/api/v2/products/999", HttpStatusCode.NotFound, "product_not_found"));
    await Check("40 constraint int dla produktu -> 404", () => ExpectStatusOnly("/api/v2/products/nope", HttpStatusCode.NotFound));
    await Check("41 nieistniejaca wersja v3 -> 404", () => ExpectStatusOnly("/api/v3/products", HttpStatusCode.NotFound));
    await Check("42 wszystkie bledy domenowe zawieraja opis", async () =>
    {
        using var response = await client.GetAsync("/users/999");
        ExpectStatus(response, HttpStatusCode.NotFound);
        using var json = await ReadJson(response);
        Expect(!string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()), "Missing message.");
    });
}
catch (Exception ex)
{
    failed++;
    Console.Error.WriteLine($"FATAL: {ex.Message}");
    lock (output) Console.Error.WriteLine(output.ToString());
}
finally
{
    if (!api.HasExited)
    {
        api.Kill(entireProcessTree: true);
        await api.WaitForExitAsync();
    }
}

Console.WriteLine($"HTTP integration tests: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

async Task Check(string name, Func<Task> test)
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

async Task ExpectStatusOnly(string path, HttpStatusCode status)
{
    using var response = await client.GetAsync(path);
    ExpectStatus(response, status);
}

async Task ExpectError(string path, HttpStatusCode status, string errorCode)
{
    using var response = await client.GetAsync(path);
    ExpectStatus(response, status);
    using var json = await ReadJson(response);
    Expect(json.RootElement.GetProperty("code").GetString() == errorCode, "Wrong error code.");
    Expect(!string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()), "Missing error description.");
}

static void ExpectStatus(HttpResponseMessage response, HttpStatusCode expected) =>
    Expect(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}.");

static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
{
    Expect(response.Content.Headers.ContentType?.MediaType == "application/json", "Expected JSON content type.");
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}

static void ExpectKeys(JsonElement obj, params string[] keys)
{
    var actual = obj.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
    var expected = keys.Order(StringComparer.Ordinal).ToArray();
    Expect(actual.SequenceEqual(expected, StringComparer.Ordinal),
        $"Unexpected JSON keys: {string.Join(", ", actual)}; expected: {string.Join(", ", expected)}.");
}

static void Expect(bool success, string description)
{
    if (!success) throw new InvalidOperationException(description);
}

static int FindFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int result = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return result;
}

static string FindApiProject()
{
    DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        string project = Path.Combine(dir.FullName, "week02", "src", "Week02.Api", "Week02.Api.csproj");
        if (File.Exists(project)) return project;
        dir = dir.Parent;
    }

    throw new FileNotFoundException("Start the integration tests from the repository directory (or its subdirectory).");
}

static Process StartApi(string project, string url, StringBuilder output)
{
    var info = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(project)!
    };
    foreach (string arg in new[] { "run", "--project", project, "--no-launch-profile", "--", $"--urls={url}" })
        info.ArgumentList.Add(arg);

    Process process = Process.Start(info) ?? throw new InvalidOperationException("Unable to start dotnet.");
    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is not null) lock (output) output.AppendLine(eventArgs.Data);
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is not null) lock (output) output.AppendLine(eventArgs.Data);
    };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static async Task WaitUntilReady(HttpClient client, Process api, StringBuilder output)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(45);
    while (DateTime.UtcNow < deadline)
    {
        if (api.HasExited)
        {
            lock (output) throw new InvalidOperationException($"API exited unexpectedly:\n{output}");
        }

        try
        {
            using var response = await client.GetAsync("/users");
            if (response.IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }

        await Task.Delay(200);
    }

    lock (output) throw new TimeoutException($"API did not start:\n{output}");
}
