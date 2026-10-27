using Week02.Api.Domain;

namespace Week02.Api.Repositories;

public interface IUserDirectory
{
    IReadOnlyList<User> GetAll();
    User? GetById(int id);
}

public sealed class SeededUserDirectory : IUserDirectory
{
    private static readonly IReadOnlyList<User> Users = Array.AsReadOnly(new[]
    {
        new User(1, "Anna Kowalska", "anna.kowalska@example.com"),
        new User(2, "Jan Nowak", "jan.nowak@example.com"),
        new User(3, "Maria Wisniewska", "maria.wisniewska@example.com"),
        new User(4, "Piotr Zielinski", "piotr.zielinski@example.com"),
        new User(5, "Katarzyna Wozniak", "katarzyna.wozniak@example.com"),
        new User(6, "Pawel Mazur", "pawel.mazur@example.com"),
        new User(7, "Ewa Lewandowska", "ewa.lewandowska@example.com"),
        new User(8, "Adam Kaminski", "adam.kaminski@example.com"),
        new User(9, "Zofia Dabrowska", "zofia.dabrowska@example.com"),
        new User(10, "Tomasz Krawczyk", "tomasz.krawczyk@example.com"),
        new User(11, "Anna Lis", "anna.lis@example.com"),
        new User(12, "Robert Grabowski", "robert.grabowski@example.com")
    });

    public IReadOnlyList<User> GetAll() => Users;
    public User? GetById(int id) => Users.FirstOrDefault(user => user.Id == id);
}

public interface ITaskCatalog
{
    IReadOnlyList<WorkTask> GetAll();
    WorkTask? GetById(int id);
}

public sealed class SeededTaskCatalog : ITaskCatalog
{
    private static readonly IReadOnlyList<WorkTask> Tasks = Array.AsReadOnly(new[]
    {
        new WorkTask(1, "Przygotowac projekt API", true, 1),
        new WorkTask(2, "Dodac walidacje tras", false, 2),
        new WorkTask(3, "Dodac testy integracyjne", false, 11)
    });

    public IReadOnlyList<WorkTask> GetAll() => Tasks;
    public WorkTask? GetById(int id) => Tasks.FirstOrDefault(task => task.Id == id);
}

public interface IProductCatalog
{
    IReadOnlyList<Product> GetAll();
    Product? GetById(int id);
}

public sealed class SeededProductCatalog : IProductCatalog
{
    // Jedno źródło prawdy — wykorzystywane przez obie wersje API.
    private static readonly IReadOnlyList<Product> Products = Array.AsReadOnly(new[]
    {
        new Product(1, "Klawiatura", 149.90m, "PLN", "Akcesoria", true),
        new Product(2, "Mysz", 89.50m, "PLN", "Akcesoria", true),
        new Product(3, "Monitor", 899.00m, "PLN", "Monitory", false),
        new Product(4, "Sluchawki", 239.99m, "PLN", "Audio", true)
    });

    public IReadOnlyList<Product> GetAll() => Products;
    public Product? GetById(int id) => Products.FirstOrDefault(product => product.Id == id);
}
