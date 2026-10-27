using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var project = LocateProject();
var port = AllocatePort();
var baseUri = new Uri($"http://127.0.0.1:{port}");
using var http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(5) };
var primaryBytes = RandomNumberGenerator.GetBytes(64);
var oldKey = Convert.ToBase64String(primaryBytes);
var adminEmail = "admin-" + Guid.NewGuid().ToString("N") + "@example.test";
var adminPassword = "A!" + Guid.NewGuid().ToString("N") + "_secure";
var password = "B!" + Guid.NewGuid().ToString("N") + "_secure";
var secondPassword = "C!" + Guid.NewGuid().ToString("N") + "_secure";
var aliceEmail = "alice@example.test";
var bobEmail = "bob@example.test";
var originalIssuer = "wwsi-week07-api";
var originalAudience = "wwsi-week07-client";
Server? server = null;
var passed = 0;
var failed = 0;
string? adminToken = null;
string? aliceToken = null;
string? bobToken = null;
int aliceId = 0;
int bobId = 0;

try
{
    server = await Server.Start(project, port, oldKey, adminEmail, adminPassword, http);

    await Check("01 health jest publiczny", async () =>
    {
        using var r = await Get("/health");
        Status(r, 200);
        using var json = await Parse(r);
        Assert(json.RootElement.GetProperty("status").GetString() == "healthy", "Status health.");
    });
    await Check("02 /api/users/me bez tokena → 401", () => ExpectStatus("/api/users/me", 401));
    await Check("03 /api/users bez tokena → 401", () => ExpectStatus("/api/users", 401));
    await Check("04 DELETE bez tokena → 401", () => ExpectStatus("/api/users/42", 401, HttpMethod.Delete));
    await Check("05 rejestracja poprawna → 201 + Location", async () =>
    {
        using var r = await Post("/api/auth/register", NewRegistration(aliceEmail, password));
        Status(r, 201);
        using var json = await Parse(r);
        aliceId = json.RootElement.GetProperty("id").GetInt32();
        Assert(aliceId > 1, "ID Alice niepoprawne.");
        Assert(r.Headers.Location?.ToString() == $"/api/users/{aliceId}", "Błędny Location.");
    });
    await Check("06 rejestracja nie ujawnia hasła ani hasha", async () =>
    {
        using var r = await Post("/api/auth/register", NewRegistration(bobEmail, secondPassword));
        Status(r, 201);
        var body = await r.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        bobId = json.RootElement.GetProperty("id").GetInt32();
        ExpectKeys(json.RootElement, "id", "email", "role");
        Assert(!body.Contains(secondPassword, StringComparison.Ordinal) &&
               !body.Contains("password", StringComparison.OrdinalIgnoreCase) &&
               !body.Contains("hash", StringComparison.OrdinalIgnoreCase), "Hasło/hash ujawnione.");
    });
    await Check("07 nowy użytkownik ma wyłącznie rolę User", async () =>
    {
        using var r = await Post("/api/auth/register", NewRegistration("charlie@example.test", password, "Admin"));
        Status(r, 201);
        using var json = await Parse(r);
        Assert(json.RootElement.GetProperty("role").GetString() == "User", "Eskalacja roli przez POST.");
    });
    await Check("08 rejestracja duplikatu e-mail → 409", async () =>
    {
        using var r = await Post("/api/auth/register", NewRegistration(aliceEmail, password));
        Status(r, 409);
    });
    await Check("09 rejestracja e-mail case insensitive → 409", async () =>
    {
        using var r = await Post("/api/auth/register", NewRegistration(aliceEmail.ToUpperInvariant(), password));
        Status(r, 409);
    });
    await Check("10 rejestracja brak e-mail → 400", () => InvalidRegister(new { password }, "email"));
    await Check("11 rejestracja brak hasła → 400", () => InvalidRegister(new { email = "missing@example.test" }, "password"));
    await Check("12 rejestracja krótki password → 400", () => InvalidRegister(NewRegistration("short@example.test", "short"), "password"));
    await Check("13 rejestracja >128 znaków hasła → 400", () => InvalidRegister(NewRegistration("long@example.test", new string('x', 129)), "password"));
    await Check("14 rejestracja niepoprawny email → 400", () => InvalidRegister(NewRegistration("nonsense", password), "email"));
    await Check("15 rejestracja pusty obiekt → dwa błędy", () => InvalidRegister(new { }, "email", "password"));
    await Check("16 rejestracja null e-mail → 400", () => InvalidRegister(new { email = (string?)null, password }, "email"));
    await Check("17 błędy walidacji nie odsyłają hasła", async () =>
    {
        var secret = new string('z', 150);
        using var r = await Post("/api/auth/register", NewRegistration("secret@example.test", secret));
        Status(r, 400);
        Assert(!(await r.Content.ReadAsStringAsync()).Contains(secret, StringComparison.Ordinal), "Echo hasła w błędzie.");
    });
    await Check("18 poprawne logowanie Alice → JWT", async () =>
    {
        using var r = await Post("/api/auth/login", NewLogin(aliceEmail, password));
        Status(r, 200);
        using var json = await Parse(r);
        aliceToken = json.RootElement.GetProperty("accessToken").GetString();
        Assert(aliceToken?.Split('.').Length == 3, "JWT musi mieć 3 części.");
        Assert(json.RootElement.GetProperty("tokenType").GetString() == "Bearer", "Zły typ tokena.");
    });
    await Check("19 poprawne logowanie Boba → JWT", async () =>
    {
        using var r = await Post("/api/auth/login", NewLogin(bobEmail, secondPassword));
        Status(r, 200);
        using var json = await Parse(r);
        bobToken = json.RootElement.GetProperty("accessToken").GetString();
        Assert(bobToken?.Split('.').Length == 3, "Brak JWT.");
    });
    await Check("20 logowanie admina → JWT", async () =>
    {
        using var r = await Post("/api/auth/login", NewLogin(adminEmail, adminPassword));
        Status(r, 200);
        using var json = await Parse(r);
        adminToken = json.RootElement.GetProperty("accessToken").GetString();
        Assert(adminToken?.Split('.').Length == 3, "Brak JWT Admin.");
    });
    await Check("21 zły login → 401", () => InvalidLogin("nobody@example.test", password));
    await Check("22 złe hasło → 401", () => InvalidLogin(aliceEmail, "wrongpassword1234"));
    await Check("23 brak hasła w logowaniu → 401", async () =>
    {
        using var r = await Post("/api/auth/login", new { email = aliceEmail });
        Status(r, 401);
    });
    await Check("24 brak e-mail w logowaniu → 401", async () =>
    {
        using var r = await Post("/api/auth/login", new { password });
        Status(r, 401);
    });
    await Check("25 identyczny komunikat 401 dla nieistniejącego/złego hasła", async () =>
    {
        using var a = await Post("/api/auth/login", NewLogin("nobody@example.test", password));
        using var b = await Post("/api/auth/login", NewLogin(aliceEmail, "wrongpassword1234"));
        Status(a, 401); Status(b, 401);
        using var ja = await Parse(a);
        using var jb = await Parse(b);
        Assert(ja.RootElement.GetProperty("title").GetString() == jb.RootElement.GetProperty("title").GetString() &&
               ja.RootElement.GetProperty("status").GetInt32() == jb.RootElement.GetProperty("status").GetInt32(),
               "Różne komunikaty 401.");
    });
    await Check("26 odpowiedź login ma cache-control no-store", async () =>
    {
        using var r = await Post("/api/auth/login", NewLogin(aliceEmail, password));
        Status(r, 200);
        Assert(r.Headers.CacheControl?.NoStore == true, "Brak no-store.");
    });
    await Check("27 JWT ma sub, email, role, iss, aud, exp, iat", () => Sync(() =>
    {
        using var payload = TokenPayload(aliceToken!);
        var obj = payload.RootElement;
        foreach (var key in new[] { "sub", "email", "role", "iss", "aud", "exp", "iat" })
            Assert(obj.TryGetProperty(key, out _), $"Brak claim {key}");
        Assert(obj.GetProperty("sub").GetString() == aliceId.ToString(), "Nieprawidłowy sub.");
        Assert(obj.GetProperty("role").GetString() == "User", "Nieprawidłowa rola.");
    }));
    await Check("28 JWT alg HS256 i ma kid", () => Sync(() =>
    {
        using var header = JsonDocument.Parse(DecodePart(aliceToken!.Split('.')[0]));
        Assert(header.RootElement.GetProperty("alg").GetString() == "HS256", "Niewłaściwy algorytm.");
        Assert(header.RootElement.GetProperty("kid").GetString() is { Length: > 0 }, "Brak identyfikatora klucza.");
    }));
    await Check("29 JWT expiry ≤ 15 minut + 1 sekunda", () => Sync(() =>
    {
        using var payload = TokenPayload(aliceToken!);
        var obj = payload.RootElement;
        Assert(obj.GetProperty("exp").GetInt64() - obj.GetProperty("iat").GetInt64() is >= 899 and <= 901, "Zły expiry.");
    }));
    await Check("30 poprawny token Alice daje dostęp do /me", async () =>
    {
        using var r = await Get("/api/users/me", aliceToken);
        Status(r, 200);
        using var json = await Parse(r);
        Assert(json.RootElement.GetProperty("id").GetInt32() == aliceId, "Zły użytkownik /me.");
    });
    await Check("31 token Bob jest przypisany do Boba", async () =>
    {
        using var r = await Get("/api/users/me", bobToken);
        Status(r, 200);
        using var json = await Parse(r);
        Assert(json.RootElement.GetProperty("id").GetInt32() == bobId, "Zły Bob.");
    });
    await Check("32 brak Bearer przy przypadkowym JWT → 401", () => ExpectStatus("/api/users/me", 401));
    await Check("33 bełkot jako Bearer → 401", () => ExpectStatus("/api/users/me", 401, bearer: "invalid"));
    await Check("34 zmodyfikowana sygnatura JWT → 401", async () =>
    {
        var parts = aliceToken!.Split('.');
        var mutatedSig = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        await ExpectStatus("/api/users/me", 401, bearer: parts[0] + "." + parts[1] + "." + mutatedSig);
    });
    await Check("35 JWT podpisany obcym kluczem → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(RandomNumberGenerator.GetBytes(64), aliceId, aliceEmail, "User")));
    await Check("36 nieprawidłowy issuer → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(primaryBytes, aliceId, aliceEmail, "User", issuer: "evil-issuer")));
    await Check("37 nieprawidłowy audience → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(primaryBytes, aliceId, aliceEmail, "User", audience: "evil-audience")));
    await Check("38 token wygasły ponad clock skew → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(primaryBytes, aliceId, aliceEmail, "User", expiresInSeconds: -90)));
    await Check("39 token nbf daleko w przyszłości → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(primaryBytes, aliceId, aliceEmail, "User", notBeforeSeconds: 300)));
    await Check("40 nieznany subject → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(primaryBytes, 9999999, aliceEmail, "User")));
    await Check("41 sfałszowana rola Admin dla Alice → 401", () =>
        ExpectStatus("/api/users/me", 401, bearer: Forge(primaryBytes, aliceId, aliceEmail, "Admin")));
    await Check("42 user lista użytkowników → 403", () => ExpectStatus("/api/users", 403, bearer: aliceToken));
    await Check("43 user DELETE → 403", () => ExpectStatus($"/api/users/{bobId}", 403, HttpMethod.Delete, aliceToken));
    await Check("44 Bob DELETE → 403", () => ExpectStatus($"/api/users/{aliceId}", 403, HttpMethod.Delete, bobToken));
    await Check("45 admin może wyświetlić użytkowników → 200", async () =>
    {
        using var r = await Get("/api/users", adminToken);
        Status(r, 200);
        using var json = await Parse(r);
        Assert(json.RootElement.GetArrayLength() == 4, "Powinno być czterech użytkowników.");
    });
    await Check("46 lista admina nie wycieka hashy", async () =>
    {
        using var r = await Get("/api/users", adminToken);
        Status(r, 200);
        var body = await r.Content.ReadAsStringAsync();
        Assert(!body.Contains("password", StringComparison.OrdinalIgnoreCase) &&
               !body.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
               !body.Contains(password, StringComparison.Ordinal), "Wycieka hash lub hasło.");
    });
    await Check("47 admin DELETE nieznanego ID → 404", () => ExpectStatus("/api/users/999999", 404, HttpMethod.Delete, adminToken));
    await Check("48 admin nie usuwa samego siebie → 409", () => ExpectStatus("/api/users/1", 409, HttpMethod.Delete, adminToken));
    await Check("49 admin usuwa Boba → 204", () => ExpectStatus($"/api/users/{bobId}", 204, HttpMethod.Delete, adminToken));
    await Check("50 usunięty Bob nie może użyć starego tokena → 401", () => ExpectStatus("/api/users/me", 401, bearer: bobToken));
    await Check("51 usunięty Bob nie może się zalogować → 401", () => InvalidLogin(bobEmail, secondPassword));
    await Check("52 ponowne usunięcie Boba → 404", () => ExpectStatus($"/api/users/{bobId}", 404, HttpMethod.Delete, adminToken));
    await Check("53 wolny e-mail Boba może zostać użyty ponownie", async () =>
    {
        using var r = await Post("/api/auth/register", NewRegistration(bobEmail, secondPassword));
        Status(r, 201);
    });
    await Check("54 token bez podpisu → 401", () => ExpectStatus("/api/users/me", 401, bearer: "eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0."));
    await Check("55 health ignoruje token", () => ExpectStatus("/health", 200, bearer: "invalid"));

    // Rotation: re-bootstrap the same admin with the same identity; old token remains valid
    // only while previous verification key is still explicitly configured.
    await server.DisposeAsync();
    server = null;
    var newerKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    server = await Server.Start(project, port, newerKey, adminEmail, adminPassword, http, oldKey);
    await Check("56 rotacja: stary JWT działa z previous signing key", () => ExpectStatus("/api/users/me", 200, bearer: adminToken));
    await Check("57 rotacja: nowy JWT również działa", async () =>
    {
        using var login = await Post("/api/auth/login", NewLogin(adminEmail, adminPassword));
        Status(login, 200);
        using var json = await Parse(login);
        await ExpectStatus("/api/users/me", 200, bearer: json.RootElement.GetProperty("accessToken").GetString());
    });
    await Check("58 rotacja: admin zachowuje uprawnienia", () => ExpectStatus("/api/users", 200, bearer: adminToken));
    await server.DisposeAsync();
    server = null;
    server = await Server.Start(project, port, newerKey, adminEmail, adminPassword, http);
    await Check("59 po usunięciu previous key stary JWT → 401", () => ExpectStatus("/api/users/me", 401, bearer: adminToken));
    await Check("60 health po rotacji nadal publiczny", () => ExpectStatus("/health", 200));
}
catch (Exception error)
{
    failed++;
    Console.Error.WriteLine("FATAL: " + error.Message);
    if (server is not null) Console.Error.WriteLine(server.Log);
}
finally
{
    if (server is not null) await server.DisposeAsync();
}
Console.WriteLine($"WEEK07: {passed} zaliczonych, {failed} niezaliczonych z 60 testów.");
return failed == 0 && passed == 60 ? 0 : 1;

async Task Check(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.WriteLine("PASS " + name);
        passed++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("FAIL " + name + ": " + ex.Message);
        failed++;
    }
}

static Task Sync(Action action) { action(); return Task.CompletedTask; }
static void Assert(bool condition, string error)
{
    if (!condition) throw new InvalidOperationException(error);
}
static void Status(HttpResponseMessage response, int expected) =>
    Assert((int)response.StatusCode == expected, $"Oczekiwano HTTP {expected}, uzyskano {(int)response.StatusCode}.");
static Task<JsonDocument> Parse(HttpResponseMessage response) => ParseContent(response.Content);
static async Task<JsonDocument> ParseContent(HttpContent content) =>
    JsonDocument.Parse(await content.ReadAsStringAsync());
static object NewRegistration(string email, string pwd, string? role = null) => new { email, password = pwd, role };
static object NewLogin(string email, string pwd) => new { email, password = pwd };

async Task<HttpResponseMessage> Post(string path, object request) => await http.PostAsJsonAsync(path, request);
async Task<HttpResponseMessage> Get(string path, string? bearer = null) =>
    await Send(path, HttpMethod.Get, bearer);
async Task<HttpResponseMessage> Send(string path, HttpMethod method, string? bearer)
{
    using var request = new HttpRequestMessage(method, path);
    if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
    return await http.SendAsync(request);
}
async Task ExpectStatus(string path, int code, HttpMethod? method = null, string? bearer = null)
{
    using var response = await Send(path, method ?? HttpMethod.Get, bearer);
    Status(response, code);
}
async Task InvalidLogin(string email, string pwd)
{
    using var r = await Post("/api/auth/login", NewLogin(email, pwd));
    Status(r, 401);
    using var doc = await Parse(r);
    Assert(doc.RootElement.GetProperty("status").GetInt32() == 401, "ProblemDetails 401.");
}
async Task InvalidRegister(object request, params string[] fields)
{
    using var r = await Post("/api/auth/register", request);
    Status(r, 400);
    Assert(r.Content.Headers.ContentType?.MediaType == "application/problem+json", "Brak application/problem+json.");
    using var doc = await Parse(r);
    var errors = doc.RootElement.GetProperty("errors");
    foreach (var field in fields) Assert(errors.TryGetProperty(field, out _), "Brak błędu: " + field);
}
static void ExpectKeys(JsonElement element, params string[] keys)
{
    var actual = element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
    Assert(actual.SequenceEqual(keys.Order(StringComparer.Ordinal)), "Nieoczekiwane pola JSON.");
}
static JsonDocument TokenPayload(string jwt) =>
    JsonDocument.Parse(DecodePart(jwt.Split('.')[1]));
static byte[] DecodePart(string encoded)
{
    var padded = encoded.Replace('-', '+').Replace('_', '/');
    return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
}
static string B64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
string Forge(byte[] key, int id, string email, string role,
    string? issuer = null, string? audience = null,
    long expiresInSeconds = 900, long notBeforeSeconds = 0)
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
    var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
    {
        ["iss"] = issuer ?? originalIssuer, ["aud"] = audience ?? originalAudience,
        ["sub"] = id.ToString(), ["email"] = email, ["role"] = role,
        ["iat"] = now, ["nbf"] = now + notBeforeSeconds, ["exp"] = now + expiresInSeconds,
        ["jti"] = Guid.NewGuid().ToString("N")
    }));
    var message = header + "." + payload;
    return message + "." + B64(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(message)));
}
static string LocateProject()
{
    DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        var project = Path.Combine(directory.FullName, "week07/src/Week07.Api/Week07.Api.csproj");
        if (File.Exists(project)) return project;
        directory = directory.Parent;
    }
    throw new FileNotFoundException("Uruchom z repozytorium zawierającego week07/src/Week07.Api.");
}
static int AllocatePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

sealed class Server : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _log = new();
    public string Log { get { lock (_log) return _log.ToString(); } }
    private Server(Process process)
    {
        _process = process;
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
    private void Append(string? value)
    {
        if (value is null) return;
        lock (_log) if (_log.Length < 20_000) _log.AppendLine(value);
    }
    public static async Task<Server> Start(string project, int port,
        string base64Key, string email, string password, HttpClient client, string? previous = null)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(project)!
        };
        foreach (var arg in new[] { "run", "--project", project, "--configuration", "Release",
            "--no-build", "--no-launch-profile", "--", $"--urls=http://127.0.0.1:{port}" })
            info.ArgumentList.Add(arg);
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["Jwt__SigningKey"] = base64Key;
        info.Environment["BootstrapAdmin__Email"] = email;
        info.Environment["BootstrapAdmin__Password"] = password;
        if (previous is not null) info.Environment["Jwt__PreviousSigningKeys__0"] = previous;
        var process = Process.Start(info) ?? throw new InvalidOperationException("Nie udało się uruchomić API.");
        var result = new Server(process);
        try
        {
            for (var i = 0; i < 150; i++)
            {
                if (process.HasExited) throw new InvalidOperationException("Serwer zakończył działanie. " + result.Log);
                try
                {
                    using var r = await client.GetAsync("/health");
                    if (r.StatusCode == HttpStatusCode.OK) return result;
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(150);
            }
            throw new TimeoutException("Timeout startup. " + result.Log);
        }
        catch
        {
            await result.DisposeAsync();
            throw;
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process.Dispose();
    }
}
