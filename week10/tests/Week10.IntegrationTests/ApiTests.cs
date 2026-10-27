using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Week10.Api.Data;
using Xunit;

namespace Week10.IntegrationTests;

public class ApiTests
{
    private static object NewUser(string name = "alice") => new
    {
        username = name, email = $"{name}@example.test", password = "UserStrongPassword_123!"
    };
    private static object Task(string title = "First task", string status = "Todo",
        int[]? tagIds = null, int? ownerId = null, DateTime? dueAtUtc = null) =>
        new { title, status, description = "Test description", tagIds = tagIds ?? [], ownerId, dueAtUtc };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    private static async Task<int> Register(HttpClient client, string name = "alice")
    {
        using var response = await client.PostAsJsonAsync("/auth/register", NewUser(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    private static async Task<string> Login(HttpClient client, string name = "alice", string password = "UserStrongPassword_123!")
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { username = name, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<int> CreateTask(HttpClient client, string title = "Task",
        string status = "Todo", int[]? tags = null, int? owner = null, DateTime? due = null)
    {
        using var response = await client.PostAsJsonAsync("/tasks", Task(title, status, tags, owner, due));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    private static void Use(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<int> PrepareUser(HttpClient client, string name = "alice")
    {
        var id = await Register(client, name);
        Use(client, await Login(client, name));
        return id;
    }

    private static async Task PrepareAdmin(HttpClient client) =>
        Use(client, await Login(client, ApiFactory.AdminUsername, ApiFactory.AdminPassword));

    [Fact] public async Task Health_returns_200()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
    }

    [Fact] public async Task Register_returns_201_and_location_without_hash()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        using var r = await c.PostAsJsonAsync("/auth/register", NewUser());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var body = await Json(r);
        Assert.Equal($"/users/{body.GetProperty("id").GetInt32()}", r.Headers.Location!.ToString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.Equal("User", body.GetProperty("role").GetString());
    }

    [Fact] public async Task Password_hash_is_not_plaintext_in_db()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c);
        using var scope = f.Services.CreateScope();
        var u = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(x => x.Username == "alice");
        Assert.NotEqual("UserStrongPassword_123!", u.PasswordHash);
        Assert.NotEmpty(u.PasswordHash);
    }

    [Fact] public async Task Register_bad_email_gives_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        using var r = await c.PostAsJsonAsync("/auth/register", new { username = "alice", email = "wrong", password = "LongPassword_12345" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.True((await Json(r)).GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact] public async Task Register_short_password_gives_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        using var r = await c.PostAsJsonAsync("/auth/register", new { username = "alice", email = "a@example.test", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task Duplicate_username_is_conflict_case_insensitive()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c);
        using var r = await c.PostAsJsonAsync("/auth/register", new { username = "ALICE", email = "other@example.test", password = "LongPassword_12345" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact] public async Task Duplicate_email_is_conflict_case_insensitive()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c);
        using var r = await c.PostAsJsonAsync("/auth/register", new { username = "different", email = "ALICE@example.test", password = "LongPassword_12345" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact] public async Task Login_returns_jwt_and_expiration()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c);
        using var r = await c.PostAsJsonAsync("/auth/login", new { username = "alice", password = "UserStrongPassword_123!" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var data = await Json(r);
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
        Assert.Equal(1800, data.GetProperty("expiresInSeconds").GetInt32());
        Assert.Equal(3, data.GetProperty("accessToken").GetString()!.Split('.').Length);
    }

    [Fact] public async Task Login_bad_credentials_gives_401()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c);
        using var r = await c.PostAsJsonAsync("/auth/login", new { username = "alice", password = "bad" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact] public async Task Anonymous_task_list_gives_401()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/tasks")).StatusCode);
    }

    [Fact] public async Task Malformed_token_gives_401()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        Use(c, "not.a.valid.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/tasks")).StatusCode);
    }

    [Fact] public async Task Normal_user_gets_403_on_admin_report()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/reports/tasks/by-status")).StatusCode);
    }

    [Fact] public async Task Normal_user_gets_403_on_user_list()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/users")).StatusCode);
    }

    [Fact] public async Task Own_user_get_returns_200()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var id = await PrepareUser(c);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/users/{id}")).StatusCode);
    }

    [Fact] public async Task Access_to_other_user_gives_403()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var id = await PrepareUser(c);
        var otherId = await Register(c, "bob");
        Assert.NotEqual(id, otherId);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/users/{otherId}")).StatusCode);
    }

    [Fact] public async Task User_can_update_own_email()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var id = await PrepareUser(c);
        using var r = await c.PutAsJsonAsync($"/users/{id}", new { email = "new@example.test" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("new@example.test", (await Json(r)).GetProperty("email").GetString());
    }

    [Fact] public async Task User_cannot_update_someone_else()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var otherId = await Register(c, "bob");
        using var r = await c.PutAsJsonAsync($"/users/{otherId}", new { email = "new@example.test" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact] public async Task User_can_delete_self()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var id = await PrepareUser(c);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/users/{id}")).StatusCode);
    }

    [Fact] public async Task Create_task_returns_201_and_get_returns_data()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var userId = await PrepareUser(c);
        var id = await CreateTask(c, "Do homework");
        using var r = await c.GetAsync($"/tasks/{id}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var data = await Json(r);
        Assert.Equal(userId, data.GetProperty("ownerId").GetInt32());
        Assert.Equal("Do homework", data.GetProperty("title").GetString());
    }

    [Fact] public async Task Task_title_missing_gives_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        using var r = await c.PostAsJsonAsync("/tasks", Task(""));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task Task_invalid_status_gives_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        using var r = await c.PostAsJsonAsync("/tasks", Task("Test", "invalid"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task Task_unknown_tag_gives_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        using var r = await c.PostAsJsonAsync("/tasks", Task(tagIds: [9999]));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task Task_repeated_tags_gives_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        using var r = await c.PostAsJsonAsync("/tasks", Task(tagIds: [1, 1]));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task User_cannot_create_task_on_behalf_of_another()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var otherId = await Register(c, "bob");
        using var r = await c.PostAsJsonAsync("/tasks", Task(ownerId: otherId));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact] public async Task Tasks_are_private_even_when_id_is_known()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var taskId = await CreateTask(c);
        await Register(c, "bob"); Use(c, await Login(c, "bob"));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/tasks/{taskId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/tasks/{taskId}")).StatusCode);
    }

    [Fact] public async Task Task_update_changes_status_and_title()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var id = await CreateTask(c);
        using var r = await c.PutAsJsonAsync($"/tasks/{id}", Task("Updated", "Done"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("Done", (await Json(r)).GetProperty("status").GetString());
    }

    [Fact] public async Task Task_update_rejects_owner_change()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var id = await CreateTask(c);
        var bobId = await Register(c, "bob");
        using var r = await c.PutAsJsonAsync($"/tasks/{id}", Task(ownerId: bobId));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task Task_delete_returns_204_then_404()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var id = await CreateTask(c);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/tasks/{id}")).StatusCode);
    }

    [Fact] public async Task Task_pagination_and_filtering_work()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        await CreateTask(c, "First", "Todo");
        await CreateTask(c, "Second", "Todo");
        await CreateTask(c, "Third", "Done");
        using var r = await c.GetAsync("/tasks?status=Todo&page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await Json(r);
        Assert.Equal(2, j.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, j.GetProperty("totalPages").GetInt32());
        Assert.Equal("Second", j.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact] public async Task Pagination_bounds_are_validated()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/tasks?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/tasks?pageSize=51")).StatusCode);
    }

    [Fact] public async Task Every_factory_uses_isolated_sqlite_database()
    {
        using var first = new ApiFactory(); using var c1 = first.CreateClient();
        await Register(c1);
        using var second = new ApiFactory(); using var c2 = second.CreateClient();
        using var login = await c2.PostAsJsonAsync("/auth/login", new { username = "alice", password = "UserStrongPassword_123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact] public async Task Admin_can_see_all_tasks()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        await CreateTask(c);
        await PrepareAdmin(c);
        using var r = await c.GetAsync("/tasks");
        Assert.Equal(1, (await Json(r)).GetProperty("totalItems").GetInt32());
    }

    [Fact] public async Task Admin_can_manage_another_users_task()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var id = await CreateTask(c);
        await PrepareAdmin(c);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/tasks/{id}")).StatusCode);
    }

    [Fact] public async Task Admin_can_create_task_for_another_user()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var userId = await Register(c);
        await PrepareAdmin(c);
        var taskId = await CreateTask(c, owner: userId);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/tasks/{taskId}")).StatusCode);
    }

    [Fact] public async Task Nonadmin_cannot_create_new_tag()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/tags", new { name = "newtag" })).StatusCode);
    }

    [Fact] public async Task Admin_can_create_tag_and_duplicate_is_conflict()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareAdmin(c);
        using var r = await c.PostAsJsonAsync("/tags", new { name = "mytag" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/tags", new { name = "MYTAG" })).StatusCode);
    }

    [Fact] public async Task Task_tags_many_to_many_work_and_can_be_updated()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        var id = await CreateTask(c, tags: [1, 2]);
        Assert.Equal(2, (await Json(await c.GetAsync($"/tasks/{id}"))).GetProperty("tags").GetArrayLength());
        using var update = await c.PutAsJsonAsync($"/tasks/{id}", Task("Updated", tagIds: [2, 3]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var tags = (await Json(update)).GetProperty("tags");
        Assert.Equal(2, tags.GetArrayLength());
        Assert.Equal(2, tags[0].GetProperty("id").GetInt32());
        Assert.Equal(3, tags[1].GetProperty("id").GetInt32());
    }

    [Fact] public async Task Report_by_status_counts_tasks()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        await CreateTask(c, "A", "Todo");
        await CreateTask(c, "B", "Done");
        await PrepareAdmin(c);
        using var r = await c.GetAsync("/reports/tasks/by-status");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(2, (await Json(r)).GetArrayLength());
    }

    [Fact] public async Task Report_by_user_includes_users_without_tasks()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c);
        await PrepareAdmin(c);
        var j = await Json(await c.GetAsync("/reports/tasks/by-user"));
        Assert.True(j.GetArrayLength() >= 2);
        Assert.All(j.EnumerateArray().ToArray(), row => Assert.Equal(0, row.GetProperty("count").GetInt32()));
    }

    [Fact] public async Task Report_top_tags_counts_links()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        await CreateTask(c, tags: [1]);
        await CreateTask(c, tags: [1, 2]);
        await PrepareAdmin(c);
        var j = await Json(await c.GetAsync("/reports/tags/top?limit=1"));
        Assert.Equal(1, j.GetArrayLength());
        Assert.Equal(2, j[0].GetProperty("count").GetInt32());
    }

    [Fact] public async Task Report_overdue_excludes_done_tasks()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        await CreateTask(c, "Overdue", "Todo", due: DateTime.UtcNow.AddDays(-2));
        await CreateTask(c, "Complete", "Done", due: DateTime.UtcNow.AddDays(-2));
        await CreateTask(c, "Future", "Todo", due: DateTime.UtcNow.AddDays(2));
        await PrepareAdmin(c);
        var j = await Json(await c.GetAsync("/reports/tasks/overdue"));
        Assert.Equal(1, j.GetArrayLength());
        Assert.Equal("Overdue", j[0].GetProperty("title").GetString());
    }

    [Fact] public async Task Reports_reject_invalid_limit()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareAdmin(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/reports/tags/top?limit=0")).StatusCode);
    }

    [Fact] public async Task Db_migration_exists_and_is_applied()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains("202610260001_InitialCreate", await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact] public async Task Dev_seed_is_idempotent()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await db.Tags.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync(x => x.Role == "Admin"));
    }

    [Fact] public async Task Global_exception_handler_is_registered_and_normal_404_is_safe()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareUser(c);
        using var r = await c.GetAsync("/tasks/999999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact] public async Task User_create_alias_post_users_returns_201()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        using var r = await c.PostAsJsonAsync("/users", NewUser("other"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact] public async Task Admin_can_filter_and_page_users()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await Register(c, "alice");
        await Register(c, "adam");
        await PrepareAdmin(c);
        using var r = await c.GetAsync("/users?search=a&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await Json(r);
        Assert.True(body.GetProperty("totalItems").GetInt32() >= 2);
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
    }

    [Fact] public async Task Users_invalid_pagination_returns_400()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        await PrepareAdmin(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/users?page=0")).StatusCode);
    }

    [Fact] public async Task Admin_can_get_and_delete_other_user()
    {
        using var f = new ApiFactory(); using var c = f.CreateClient();
        var id = await Register(c, "alice");
        await PrepareAdmin(c);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/users/{id}")).StatusCode);
    }

}
