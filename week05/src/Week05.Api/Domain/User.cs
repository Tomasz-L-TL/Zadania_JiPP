namespace Week05.Api.Domain;

public sealed class User
{
    private User() { } // EF Core materialization

    public User(string name, string email, int age)
    {
        SetProfile(name, email, age);
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public int Age { get; private set; }
    // Intentional internal field — clients cannot set or read it via DTO.
    public bool IsAdmin { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateProfile(string name, string email, int age)
    {
        SetProfile(name, email, age);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void SetProfile(string name, string email, int age)
    {
        Name = name.Trim();
        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        Age = age;
    }
}
