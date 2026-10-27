using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Week10.Api.Data;
using Week10.Api.Domain;

namespace Week10.Api.Services;

public static class DevSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher,
        IConfiguration config, IHostEnvironment env)
    {
        if (!env.IsDevelopment()) throw new InvalidOperationException("Developer seed cannot run outside Development.");
        foreach (var name in new[] { "backend", "urgent", "documentation", "testing" })
        {
            var normalized = name.ToUpperInvariant();
            if (!await db.Tags.AnyAsync(x => x.NormalizedName == normalized))
                db.Tags.Add(new Tag { Name = name, NormalizedName = normalized });
        }
        await db.SaveChangesAsync();

        // Administrator tylko po świadomym podaniu danych przez środowisko.
        var username = config["DevelopmentSeed:AdminUsername"];
        var email = config["DevelopmentSeed:AdminEmail"];
        var password = config["DevelopmentSeed:AdminPassword"];
        var configured = new[] { username, email, password }.Count(x => !string.IsNullOrWhiteSpace(x));
        if (configured == 0) return;
        if (configured != 3 || password!.Length < 12 || !System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new InvalidOperationException("Development admin needs valid username/email and password (12+ chars).");
        var upper = username!.Trim().ToUpperInvariant();
        if (!await db.Users.AnyAsync(x => x.NormalizedUsername == upper))
        {
            var admin = new User
            {
                Username = username.Trim(), NormalizedUsername = upper,
                Email = email!.Trim(), NormalizedEmail = email!.Trim().ToUpperInvariant(),
                Role = "Admin"
            };
            admin.PasswordHash = hasher.HashPassword(admin, password!);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}
