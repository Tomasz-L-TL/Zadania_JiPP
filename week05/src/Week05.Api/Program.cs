using Microsoft.EntityFrameworkCore;
using Week05.Api.Data;
using Week05.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("UsersDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Missing ConnectionStrings:UsersDb. Set ConnectionStrings__UsersDb via environment or User Secrets.");

builder.Services.AddDbContext<UsersDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Automatic schema changes are gated by environment AND configuration.
if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapUserEndpoints();
app.Run();

public partial class Program { }
