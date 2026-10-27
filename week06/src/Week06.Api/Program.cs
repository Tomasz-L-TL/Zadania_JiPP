using Microsoft.EntityFrameworkCore;
using Week06.Api.Data;
using Week06.Api.Endpoints;
using Week06.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Week06Db");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Brak ConnectionStrings:Week06Db; ustaw ConnectionStrings__Week06Db.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
var app = builder.Build();

if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
        await DevelopmentSeeder.SeedAsync(db, app.Environment);
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapRelationshipEndpoints();
app.Run();

public partial class Program { }
