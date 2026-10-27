namespace Week04.Api.Domain;

public sealed class Product
{
    // Konstruktor bezargumentowy wymagany przez EF Core do materializacji encji.
    private Product() { }

    public Product(string sku, string name, decimal price,
        string? description = null, DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU jest wymagane.", nameof(sku));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nazwa jest wymagana.", nameof(name));
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Cena nie może być ujemna.");

        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public int Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
