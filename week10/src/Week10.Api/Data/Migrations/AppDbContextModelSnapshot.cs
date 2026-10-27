using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Week10.Api.Data;
using Week10.Api.Domain;

#nullable disable

namespace Week10.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
public partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Week10.Api.Domain.User", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<string>("Username").IsRequired().HasMaxLength(50).HasColumnType("TEXT");
            b.Property<string>("NormalizedUsername").IsRequired().HasMaxLength(50).HasColumnType("TEXT");
            b.Property<string>("Email").IsRequired().HasMaxLength(254).HasColumnType("TEXT");
            b.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(254).HasColumnType("TEXT");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Role").IsRequired().HasMaxLength(20).HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("NormalizedUsername").IsUnique();
            b.HasIndex("NormalizedEmail").IsUnique();
            b.ToTable("Users");
        });
        modelBuilder.Entity("Week10.Api.Domain.Tag", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<string>("Name").IsRequired().HasMaxLength(40).HasColumnType("TEXT");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(40).HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("NormalizedName").IsUnique();
            b.ToTable("Tags");
        });
        modelBuilder.Entity("Week10.Api.Domain.TaskItem", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            b.Property<string>("Title").IsRequired().HasMaxLength(120).HasColumnType("TEXT");
            b.Property<string>("Description").HasMaxLength(2000).HasColumnType("TEXT");
            b.Property<WorkStatus>("Status").HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnType("TEXT");
            b.Property<DateTime?>("DueAtUtc").HasColumnType("TEXT");
            b.Property<int>("OwnerId").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("OwnerId", "Status");
            b.HasIndex("DueAtUtc");
            b.ToTable("Tasks");
        });
        modelBuilder.Entity("Week10.Api.Domain.TaskTag", b =>
        {
            b.Property<int>("TaskItemId").HasColumnType("INTEGER");
            b.Property<int>("TagId").HasColumnType("INTEGER");
            b.HasKey("TaskItemId", "TagId");
            b.HasIndex("TagId");
            b.ToTable("TaskTags");
        });
        modelBuilder.Entity("Week10.Api.Domain.TaskItem", b =>
        {
            b.HasOne("Week10.Api.Domain.User", "Owner")
                .WithMany("Tasks").HasForeignKey("OwnerId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Owner");
        });
        modelBuilder.Entity("Week10.Api.Domain.TaskTag", b =>
        {
            b.HasOne("Week10.Api.Domain.Tag", "Tag")
                .WithMany("TaskTags").HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne("Week10.Api.Domain.TaskItem", "TaskItem")
                .WithMany("TaskTags").HasForeignKey("TaskItemId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Tag"); b.Navigation("TaskItem");
        });
        modelBuilder.Entity("Week10.Api.Domain.User", b => b.Navigation("Tasks"));
        modelBuilder.Entity("Week10.Api.Domain.Tag", b => b.Navigation("TaskTags"));
        modelBuilder.Entity("Week10.Api.Domain.TaskItem", b => b.Navigation("TaskTags"));
    }
}
