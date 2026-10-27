using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Week04.Api.Data.Migrations;

// Model docelowy migracji — standardowa część drugiego pliku EF Core migration.
public partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Week04.Api.Domain.Product", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);

            b.Property<string>("Sku")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("TEXT");

            b.Property<string>("Description")
                .HasMaxLength(500)
                .HasColumnType("TEXT");

            b.Property<decimal>("Price")
                .HasPrecision(18, 2)
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("Sku").IsUnique();
            b.ToTable("Products");
        });
    }
}
