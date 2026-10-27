using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Week05.Api.Data.Migrations;

[DbContext(typeof(UsersDbContext))]
public partial class UsersDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Week05.Api.Domain.User", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            entity.Property<string>("Email").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            entity.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            entity.Property<int>("Age").HasColumnType("INTEGER");
            entity.Property<bool>("IsAdmin").HasColumnType("INTEGER");
            entity.Property<DateTime>("CreatedAtUtc").HasColumnType("TEXT");
            entity.Property<DateTime>("UpdatedAtUtc").HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("NormalizedEmail").IsUnique();
            entity.ToTable("Users");
        });
    }
}
