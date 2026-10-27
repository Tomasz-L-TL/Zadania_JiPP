using Week03.Api.Domain;

namespace Week03.Api.Repositories;

public interface IUserRepository
{
    // Atomiczne sprawdzenie unikalności + zapis (bez wyścigu między żądaniami).
    bool TryAdd(User candidate, out User? saved);
    User? GetById(int id);
    IReadOnlyList<User> GetAll();
}

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Lock _gate = new();
    private readonly List<User> _users = [];
    private int _nextId = 1;

    public bool TryAdd(User candidate, out User? saved)
    {
        lock (_gate)
        {
            if (_users.Any(user => string.Equals(user.Login, candidate.Login, StringComparison.OrdinalIgnoreCase)))
            {
                saved = null;
                return false;
            }

            saved = candidate with { Id = _nextId++ };
            _users.Add(saved);
            return true;
        }
    }

    public User? GetById(int id)
    {
        lock (_gate)
        {
            return _users.FirstOrDefault(user => user.Id == id);
        }
    }

    public IReadOnlyList<User> GetAll()
    {
        lock (_gate)
        {
            return _users.ToArray();
        }
    }
}
