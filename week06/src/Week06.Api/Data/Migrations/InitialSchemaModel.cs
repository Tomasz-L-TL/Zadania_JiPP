using Microsoft.EntityFrameworkCore;
using Week06.Api.Domain;

namespace Week06.Api.Data.Migrations;

// Immutable snapshot of the INITIAL schema, independent from current runtime configurations.
// Shared only by the migration designer and its matching snapshot, not by AppDbContext.
internal static class InitialSchemaModel
{
    public static void Build(ModelBuilder builder)
    {
        builder.HasAnnotation("ProductVersion", "10.0.0");

        builder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.HasKey(x => x.Id);
            user.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            user.Property(x => x.Name).IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            user.HasMany(x => x.Tasks).WithOne(x => x.User)
                .HasForeignKey(x => x.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaskItem>(task =>
        {
            task.ToTable("Tasks");
            task.HasKey(x => x.Id);
            task.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            task.Property(x => x.UserId).IsRequired().HasColumnType("INTEGER");
            task.Property(x => x.Title).IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            task.Property(x => x.IsCompleted).IsRequired().HasColumnType("INTEGER");
            task.HasIndex(x => x.UserId);
            task.HasMany(x => x.Comments).WithOne(x => x.TaskItem)
                .HasForeignKey(x => x.TaskItemId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            task.HasMany(x => x.Tags).WithMany(x => x.Tasks)
                .UsingEntity<Dictionary<string, object>>(
                    "TaskTag",
                    r => r.HasOne<Tag>().WithMany().HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade),
                    l => l.HasOne<TaskItem>().WithMany().HasForeignKey("TaskItemId")
                        .OnDelete(DeleteBehavior.Cascade),
                    join =>
                    {
                        join.ToTable("TaskTags");
                        join.HasKey("TaskItemId", "TagId");
                    });
        });

        builder.Entity<Comment>(comment =>
        {
            comment.ToTable("Comments");
            comment.HasKey(x => x.Id);
            comment.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            comment.Property(x => x.TaskItemId).IsRequired().HasColumnType("INTEGER");
            comment.Property(x => x.Text).IsRequired().HasMaxLength(1000).HasColumnType("TEXT");
            comment.HasIndex(x => x.TaskItemId);
        });

        builder.Entity<Tag>(tag =>
        {
            tag.ToTable("Tags");
            tag.HasKey(x => x.Id);
            tag.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            tag.Property(x => x.Name).IsRequired().HasMaxLength(50).HasColumnType("TEXT");
            tag.HasIndex(x => x.Name).IsUnique();
        });
    }
}
