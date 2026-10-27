using Microsoft.EntityFrameworkCore;
using Week10.Api.Domain;

namespace Week10.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var users = modelBuilder.Entity<User>();
        users.ToTable("Users");
        users.HasKey(x => x.Id);
        users.Property(x => x.Username).HasMaxLength(50).IsRequired();
        users.Property(x => x.NormalizedUsername).HasMaxLength(50).IsRequired();
        users.HasIndex(x => x.NormalizedUsername).IsUnique();
        users.Property(x => x.Email).HasMaxLength(254).IsRequired();
        users.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        users.HasIndex(x => x.NormalizedEmail).IsUnique();
        users.Property(x => x.PasswordHash).IsRequired();
        users.Property(x => x.Role).HasMaxLength(20).IsRequired();
        users.HasMany(x => x.Tasks).WithOne(x => x.Owner)
            .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);

        var tasks = modelBuilder.Entity<TaskItem>();
        tasks.ToTable("Tasks");
        tasks.HasKey(x => x.Id);
        tasks.Property(x => x.Title).HasMaxLength(120).IsRequired();
        tasks.Property(x => x.Description).HasMaxLength(2000);
        tasks.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        tasks.HasIndex(x => new { x.OwnerId, x.Status });
        tasks.HasIndex(x => x.DueAtUtc);

        var tags = modelBuilder.Entity<Tag>();
        tags.ToTable("Tags");
        tags.HasKey(x => x.Id);
        tags.Property(x => x.Name).HasMaxLength(40).IsRequired();
        tags.Property(x => x.NormalizedName).HasMaxLength(40).IsRequired();
        tags.HasIndex(x => x.NormalizedName).IsUnique();

        var join = modelBuilder.Entity<TaskTag>();
        join.ToTable("TaskTags");
        join.HasKey(x => new { x.TaskItemId, x.TagId });
        join.HasOne(x => x.TaskItem).WithMany(x => x.TaskTags)
            .HasForeignKey(x => x.TaskItemId).OnDelete(DeleteBehavior.Cascade);
        join.HasOne(x => x.Tag).WithMany(x => x.TaskTags)
            .HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        join.HasIndex(x => x.TagId);
    }
}
