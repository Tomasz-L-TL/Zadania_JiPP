using Microsoft.EntityFrameworkCore;
using Week05.Api.Domain;

namespace Week05.Api.Data;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var entity = modelBuilder.Entity<User>();
        entity.ToTable("Users");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Email).IsRequired().HasMaxLength(200);
        entity.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(200);
        entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        entity.Property(x => x.Age).IsRequired();
        entity.Property(x => x.IsAdmin).IsRequired();
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
