using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Week09.Api.Data;

namespace Week09.IntegrationTests;

/// <summary>Izolacja: nowy scenariusz = nowa WebApplicationFactory + nowa baza danych.</summary>
public sealed class ApiScenario : IDisposable
{
    public TestApiFactory Factory { get; } = new();
    public HttpClient Client { get; }

    public ApiScenario() => Client = Factory.CreateClient();

    public async Task<Guid> RegisterAsync(string email = "student@example.com", string password = "StrongPass123!")
    {
        using var response = await Client.PostAsJsonAsync("/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public async Task<string> LoginAsync(string email = "student@example.com", string password = "StrongPass123!")
    {
        using var response = await Client.PostAsJsonAsync("/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    public async Task<HttpClient> AuthorizedClientAsync(string email = "student@example.com",
        string password = "StrongPass123!")
    {
        var token = await LoginAsync(email, password);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public async Task<Guid> CreateNoteAsync(HttpClient client, string title = "Test note", string body = "Sample")
    {
        using var response = await client.PostAsJsonAsync("/api/notes", new { title, body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public async Task PromoteToAdminAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Week09DbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId);
        user.Role = "Admin";
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}
