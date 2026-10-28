using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Week10.Api.Data;
using Week10.Api.Domain;
using Week10.Api.Endpoints;
using Week10.Api.Security;
using Week10.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Read settings from DI after host configuration (including WebApplicationFactory overrides).
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var configuredConnection = services.GetRequiredService<IConfiguration>()
        .GetConnectionString("AppDb");
    if (string.IsNullOrWhiteSpace(configuredConnection))
        throw new InvalidOperationException("Configure ConnectionStrings__AppDb.");
    options.UseSqlite(configuredConnection);
});
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var jwtKey = config["Jwt:SigningKey"];
    if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
        throw new InvalidOperationException("Configure Jwt__SigningKey (at least 32 UTF-8 bytes) through a secret provider.");
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = config["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(5),
        NameClaimType = ClaimTypes.NameIdentifier, RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorizationBuilder().AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
var app = builder.Build();

// Validate required configuration after ConfigureWebHost has applied test overrides.
var jwtKey = app.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure Jwt__SigningKey (at least 32 UTF-8 bytes) through a secret provider.");
if (string.IsNullOrWhiteSpace(app.Configuration["Jwt:Issuer"]) ||
    string.IsNullOrWhiteSpace(app.Configuration["Jwt:Audience"]))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("AppDb")))
    throw new InvalidOperationException("Configure ConnectionStrings__AppDb.");

app.UseExceptionHandler();
// Nie loguj Authorization, Cookie, body ani haseł.
app.Use(async (context, next) =>
{
    var watch = Stopwatch.StartNew();
    try { await next(); }
    finally
    {
        // Nie loguj surowej ścieżki żądania, która może zawierać dane wrażliwe.
        // Wzorzec routingu (np. /tasks/{id:int}) nie ujawnia identyfikatorów.
        var route = context.GetEndpoint() is Microsoft.AspNetCore.Routing.RouteEndpoint endpoint
            ? endpoint.RoutePattern.RawText ?? "(unknown-route)"
            : "(unmatched)";
        app.Logger.LogInformation("HTTP {Method} {Route} -> {StatusCode} in {ElapsedMs}ms",
            context.Request.Method, route,
            context.Response.StatusCode, watch.ElapsedMilliseconds);
    }
});
app.UseAuthentication();
app.UseAuthorization();

// Seed i migracje tylko Development; na Production osobny krok wdrożeniowy.
if (app.Environment.IsDevelopment() && config.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (config.GetValue<bool>("Database:SeedDemoData"))
        await DevSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
            config, app.Environment);
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapTaskEndpoints();
app.MapTagEndpoints();
app.MapReportEndpoints();
app.Run();

public partial class Program { }
