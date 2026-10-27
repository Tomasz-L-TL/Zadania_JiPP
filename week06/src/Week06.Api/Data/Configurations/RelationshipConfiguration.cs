using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Week06.Api.Domain;

namespace Week06.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
        entity.HasMany(x => x.Tasks).WithOne(x => x.User)
            .HasForeignKey(x => x.UserId).IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> entity)
    {
        entity.ToTable("Tasks");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
        entity.Property(x => x.IsCompleted).IsRequired();
        entity.HasIndex(x => x.UserId);
        entity.HasMany(x => x.Comments).WithOne(x => x.TaskItem)
            .HasForeignKey(x => x.TaskItemId).IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Tabela pośrednia ma klucz złożony: pojedyncze powiązanie dla pary (task, tag).
        entity.HasMany(x => x.Tags).WithMany(x => x.Tasks)
            .UsingEntity<Dictionary<string, object>>(
                "TaskTag",
                right => right.HasOne<Tag>().WithMany()
                    .HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<TaskItem>().WithMany()
                    .HasForeignKey("TaskItemId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("TaskTags");
                    join.HasKey("TaskItemId", "TagId");
                });
    }
}

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> entity)
    {
        entity.ToTable("Comments");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Text).IsRequired().HasMaxLength(1000);
        entity.HasIndex(x => x.TaskItemId);
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> entity)
    {
        entity.ToTable("Tags");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).IsRequired().HasMaxLength(50);
        entity.HasIndex(x => x.Name).IsUnique();
    }
}
