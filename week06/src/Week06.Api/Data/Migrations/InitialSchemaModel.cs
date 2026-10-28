using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Week06.Api.Data.Migrations;

// Persisted migration model: independent from runtime configuration classes.
internal static class InitialSchemaModel
{
    public static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Week06.Api.Domain.User", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("Users");
        });

        modelBuilder.Entity("Week06.Api.Domain.Tag", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<string>("Name").IsRequired().HasMaxLength(50).HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("Name").IsUnique();
            b.ToTable("Tags");
        });

        modelBuilder.Entity("Week06.Api.Domain.TaskItem", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<int>("UserId").HasColumnType("INTEGER");
            b.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            b.Property<bool>("IsCompleted").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("UserId");
            b.ToTable("Tasks");
        });

        modelBuilder.Entity("Week06.Api.Domain.Comment", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<int>("TaskItemId").HasColumnType("INTEGER");
            b.Property<string>("Text").IsRequired().HasMaxLength(1000).HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("TaskItemId");
            b.ToTable("Comments");
        });

        modelBuilder.Entity("Week06.Api.Domain.Comment", b =>
        {
            b.HasOne("Week06.Api.Domain.TaskItem", "TaskItem")
                .WithMany("Comments")
                .HasForeignKey("TaskItemId")
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("TaskItem");
        });

        modelBuilder.Entity("Week06.Api.Domain.TaskItem", b =>
        {
            b.HasOne("Week06.Api.Domain.User", "User")
                .WithMany("Tasks")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("User");
        });

        modelBuilder.Entity("Week06.Api.Domain.TaskItem", b =>
        {
            b.HasMany("Week06.Api.Domain.Tag", "Tags")
                .WithMany("Tasks")
                .UsingEntity(
                    "TaskTag", typeof(Dictionary<string, object>),
                    r => r.HasOne("Week06.Api.Domain.Tag", null)
                        .WithMany().HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade).IsRequired(),
                    l => l.HasOne("Week06.Api.Domain.TaskItem", null)
                        .WithMany().HasForeignKey("TaskItemId")
                        .OnDelete(DeleteBehavior.Cascade).IsRequired(),
                    j =>
                    {
                        j.HasKey("TaskItemId", "TagId");
                        j.ToTable("TaskTags");
                        j.HasIndex("TagId");
                    });
            b.Navigation("Comments");
            b.Navigation("Tags");
        });

        modelBuilder.Entity("Week06.Api.Domain.User", b => b.Navigation("Tasks"));
        modelBuilder.Entity("Week06.Api.Domain.Tag", b => b.Navigation("Tasks"));
    }
}
