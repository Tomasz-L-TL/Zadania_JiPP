using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Week06.Api.Data;
using Week06.Api.Domain;

namespace Week06.Api.Seeding;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(AppDbContext db, IHostEnvironment environment,
        CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Seed dozwolony wyłącznie w Development.");
        if (await db.Users.AnyAsync(ct)) return; // idempotent development sample dataset.

        var backend = new Tag { Name = "backend" };
        var database = new Tag { Name = "database" };
        var docs = new Tag { Name = "documentation" };

        var first = new TaskItem
        {
            Title = "Implement relation mapping",
            IsCompleted = true,
            Comments =
            {
                new Comment { Text = "Check foreign keys" },
                new Comment { Text = "Review migration" }
            },
            Tags = { backend, database }
        };
        var second = new TaskItem
        {
            Title = "Write documentation",
            IsCompleted = false,
            Comments = { new Comment { Text = "Explain queries" } },
            Tags = { docs }
        };
        var third = new TaskItem
        {
            Title = "Review tests",
            IsCompleted = false,
            Tags = { backend }
        };

        db.Users.AddRange(
            new User { Name = "Anna Kowalska", Tasks = { first, second } },
            new User { Name = "Jan Nowak", Tasks = { third } });
        await db.SaveChangesAsync(ct);
    }
}
