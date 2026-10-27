using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Week04.Api.Domain;

namespace Week04.Api.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedOnAdd();

        builder.Property(product => product.Sku)
            .IsRequired()
            .HasMaxLength(32);
        builder.HasIndex(product => product.Sku).IsUnique();

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(product => product.Description)
            .HasMaxLength(500);

        // Konfiguracja precyzji EF Core. SQLite zapisuje decimal jako TEXT;
        // do ścisłego DB-side DECIMAL(18,2) zalecany jest provider SQL Server/PostgreSQL.
        builder.Property(product => product.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(product => product.CreatedAtUtc)
            .IsRequired();
    }
}
