using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Week09.Api.Data;
using Xunit;

namespace Week09.IntegrationTests;

public sealed class IntegrationTests
{
    [Fact]
    public async Task Health_returns_200_and_expected_JSON_body()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_returns_a_generated_correlation_id()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.GetAsync("/health");
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.NotEmpty(values!.Single());
    }

    [Fact]
    public async Task Valid_correlation_id_is_preserved()
    {
        using var scenario = new ApiScenario();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "e2e-42_safe");
        using var response = await scenario.Client.SendAsync(request);
        Assert.Equal("e2e-42_safe", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Invalid_correlation_id_is_replaced()
    {
        using var scenario = new ApiScenario();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "invalid value with spaces");
        using var response = await scenario.Client.SendAsync(request);
        Assert.NotEqual("invalid value with spaces", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Unknown_endpoint_returns_404_with_correlation_id()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task Anonymous_notes_collection_requires_authentication()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_create_note_requires_authentication()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.PostAsJsonAsync("/api/notes", new { title = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_user_registration_returns_201_and_public_response()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/register",
            new { email = "NEW@example.com", password = "StrongPass123!" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/auth/me", response.Headers.Location?.ToString());
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal("new@example.com", json.RootElement.GetProperty("email").GetString());
        Assert.NotEqual(Guid.Empty, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(2, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task Registration_rejects_bad_email_with_400_validation()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/register",
            new { email = "not-an-email", password = "StrongPass123!" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Registration_rejects_weak_password_with_400()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/register",
            new { email = "student@example.com", password = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Registration_rejects_missing_fields_with_multiple_errors()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/register", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Duplicate_registration_returns_409_without_inserting_data()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/register",
            new { email = "STUDENT@example.com", password = "AnotherPass123!" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Week09DbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/login",
            new { email = "student@example.com", password = "WrongPass123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_login_returns_JWT_bearer_token()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var response = await scenario.Client.PostAsJsonAsync("/auth/login",
            new { email = "student@example.com", password = "StrongPass123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal("Bearer", json.RootElement.GetProperty("tokenType").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("accessToken").GetString()!.Split('.').Length);
    }

    [Fact]
    public async Task Profile_requires_authentication()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_returns_logged_in_user()
    {
        using var scenario = new ApiScenario();
        var id = await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal(id, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("User", json.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Invalid_bearer_token_is_rejected()
    {
        using var scenario = new ApiScenario();
        using var client = scenario.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.payload");
        using var response = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Successful_POST_creates_a_note_with_201_and_Location()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.PostAsJsonAsync("/api/notes", new { title = "Hello", body = "World" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        var id = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal($"/api/notes/{id}", response.Headers.Location?.ToString());
        Assert.Equal("Hello", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_POST_returns_400_with_validation_errors()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.PostAsJsonAsync("/api/notes", new { title = "   ", body = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Oversized_note_body_returns_400()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.PostAsJsonAsync("/api/notes",
            new { title = "Valid", body = new string('x', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_note_by_id_returns_note()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(client);
        using var response = await client.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal(id, json.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Missing_note_returns_404_independent_of_data()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.GetAsync($"/api/notes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task New_factory_starts_with_empty_notes_list()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Listing_shows_new_notes_and_hides_others()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(client);
        using var response = await client.GetAsync("/api/notes");
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(id, json.RootElement[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task PUT_changes_title_and_body()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(client);
        using var response = await client.PutAsJsonAsync($"/api/notes/{id}",
            new { title = "Updated", body = "Changed" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal("Updated", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("Changed", json.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task PUT_rejects_invalid_title_and_preserves_old_value()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(client, "Original");
        using var response = await client.PutAsJsonAsync($"/api/notes/{id}", new { title = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var check = await client.GetAsync($"/api/notes/{id}");
        using var json = await ApiScenario.ReadJsonAsync(check);
        Assert.Equal("Original", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PUT_on_missing_note_returns_404()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.PutAsJsonAsync($"/api/notes/{Guid.NewGuid()}", new { title = "Valid" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_existing_note_returns_204()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(client);
        using var response = await client.DeleteAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DELETE_missing_note_returns_404()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.DeleteAsync($"/api/notes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deleted_note_is_no_longer_readable()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(client);
        using var deletion = await client.DeleteAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var response = await client.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Two_factories_have_completely_isolated_databases()
    {
        using var first = new ApiScenario();
        await first.RegisterAsync();
        using var owner = await first.AuthorizedClientAsync();
        var id = await first.CreateNoteAsync(owner);

        using var second = new ApiScenario();
        await second.RegisterAsync();
        using var other = await second.AuthorizedClientAsync();
        using var response = await other.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var list = await other.GetAsync("/api/notes");
        using var json = await ApiScenario.ReadJsonAsync(list);
        Assert.Equal(0, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Users_cannot_read_other_users_notes()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync("first@example.com");
        await scenario.RegisterAsync("second@example.com");
        using var first = await scenario.AuthorizedClientAsync("first@example.com");
        using var second = await scenario.AuthorizedClientAsync("second@example.com");
        var id = await scenario.CreateNoteAsync(first);
        using var response = await second.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Users_cannot_modify_other_users_notes()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync("first@example.com");
        await scenario.RegisterAsync("second@example.com");
        using var first = await scenario.AuthorizedClientAsync("first@example.com");
        using var second = await scenario.AuthorizedClientAsync("second@example.com");
        var id = await scenario.CreateNoteAsync(first, "OwnerTitle");
        using var response = await second.PutAsJsonAsync($"/api/notes/{id}", new { title = "Hack" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var check = await first.GetAsync($"/api/notes/{id}");
        using var json = await ApiScenario.ReadJsonAsync(check);
        Assert.Equal("OwnerTitle", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Users_cannot_delete_other_users_notes()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync("first@example.com");
        await scenario.RegisterAsync("second@example.com");
        using var first = await scenario.AuthorizedClientAsync("first@example.com");
        using var second = await scenario.AuthorizedClientAsync("second@example.com");
        var id = await scenario.CreateNoteAsync(first);
        using var response = await second.DeleteAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var ownerResponse = await first.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
    }

    [Fact]
    public async Task Ordinary_user_cannot_access_admin_endpoint()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_role_can_access_statistics()
    {
        using var scenario = new ApiScenario();
        var id = await scenario.RegisterAsync();
        await scenario.PromoteToAdminAsync(id);
        using var client = await scenario.AuthorizedClientAsync();
        using var response = await client.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("users").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("notes").GetInt32());
    }

    [Fact]
    public async Task Exception_middleware_returns_500_without_internal_error_details()
    {
        using var scenario = new ApiScenario();
        using var response = await scenario.Client.GetAsync("/__test/throw");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var json = await ApiScenario.ReadJsonAsync(response);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("expected test exception", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task Two_clients_share_the_same_factory_database()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var first = await scenario.AuthorizedClientAsync();
        using var second = await scenario.AuthorizedClientAsync();
        var id = await scenario.CreateNoteAsync(first);
        using var response = await second.GetAsync($"/api/notes/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Password_is_hashed_and_not_saved_in_plaintext()
    {
        using var scenario = new ApiScenario();
        await scenario.RegisterAsync();
        using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Week09DbContext>();
        var user = await db.Users.SingleAsync();
        Assert.NotEqual("StrongPass123!", user.PasswordHash);
        Assert.NotEmpty(user.PasswordHash);
    }
}
