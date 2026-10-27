using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Week07.Api.Auth;
using Week07.Api.Endpoints;
using Week07.Api.Users;

var builder = WebApplication.CreateBuilder(args);

// Fail closed: without a sufficiently strong secret the service cannot start.
// The signing key never appears in appsettings*.json or in source control.
var jwt = JwtSettings.Load(builder.Configuration);
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();
builder.Services.AddSingleton<IPasswordHasher<StoredUser>, PasswordHasher<StoredUser>>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.SaveToken = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwt.ValidationKeys,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        // Reject tokens for deleted accounts even before their JWT expiry.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var subject = context.Principal?.FindFirst("sub")?.Value;
                var tokenEmail = context.Principal?.FindFirst("email")?.Value;
                var tokenRole = context.Principal?.FindFirst("role")?.Value;
                var store = context.HttpContext.RequestServices.GetRequiredService<IUserStore>();
                if (!int.TryParse(subject, out var userId) ||
                    store.FindById(userId) is not { } user ||
                    !string.Equals(user.Email, tokenEmail, StringComparison.Ordinal) ||
                    !string.Equals(user.Role, tokenRole, StringComparison.Ordinal))
                    context.Fail("Konto tokena jest nieaktywne.");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

var app = builder.Build();

// Demo admin can only be bootstrapped when explicitly configured for Development.
// Never put bootstrap credentials in any tracked file or command-line argument.
var adminEmail = app.Configuration["BootstrapAdmin:Email"];
var adminPassword = app.Configuration["BootstrapAdmin:Password"];
if (adminEmail is not null || adminPassword is not null)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Bootstrap administratora jest dozwolony wyłącznie w Development.");
    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        throw new InvalidOperationException("W Development podaj oba ustawienia BootstrapAdmin:Email i Password.");
    app.Services.GetRequiredService<AuthService>().BootstrapAdmin(adminEmail, adminPassword);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program { }
