using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Week09.Api.Data;
using Week09.Api.Domain;
using Week09.Api.Endpoints;
using Week09.Api.Middleware;
using Week09.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja EF oraz JWT jest pobierana po zbudowaniu kontenera usług.
// Dzięki temu WebApplicationFactory może bezpiecznie dostarczyć testową bazę
// i osobny klucz JWT, bez sekretów w kodzie aplikacji.
builder.Services.AddDbContext<Week09DbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:Default is required.");
    options.UseSqlite(connectionString);
});
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes. Provide it securely.");

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "Week09.Api",
            ValidAudience = configuration["Jwt:Audience"] ?? "Week09.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(15)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Fail-fast w normalnych środowiskach. WebApplicationFactory udostępnia
// własną konfigurację podczas startu hosta testowego.
if (!app.Environment.IsEnvironment("Testing"))
{
    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
        throw new InvalidOperationException("ConnectionStrings:Default is required.");
    var key = app.Configuration["Jwt:SigningKey"];
    if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes. Provide it securely.");
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("ExceptionHandler")
        .LogError(exception, "Unhandled exception in request");
    await Results.Problem(statusCode: 500, title: "Wewnętrzny błąd serwera.")
        .ExecuteAsync(context);
}));

app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("Automatic EnsureCreated is not allowed in Production.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<Week09DbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapAuthEndpoints();
app.MapNoteEndpoints();

// Only available to the in-process test host, never deployed as a public endpoint.
if (app.Environment.IsEnvironment("Testing"))
    app.MapGet("/__test/throw", (Func<IResult>)(() =>
        throw new InvalidOperationException("expected test exception")));

app.Run();

public partial class Program { }
