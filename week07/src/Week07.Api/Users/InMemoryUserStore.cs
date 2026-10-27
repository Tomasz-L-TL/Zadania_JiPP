namespace Week07.Api.Users;

public interface IUserStore
{
    StoredUser? FindByEmail(string email);
    StoredUser? FindById(int id);
    StoredUser? TryCreate(string email, string passwordHash, string role);
    IReadOnlyList<StoredUser> GetAll();
    bool Delete(int id);
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly object _sync = new();
    private readonly Dictionary<int, StoredUser> _byId = new();
    private readonly Dictionary<string, int> _byEmail = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;

    public StoredUser? FindByEmail(string email)
    {
        lock (_sync)
            return _byEmail.TryGetValue(email, out var id) ? _byId[id] : null;
    }

    public StoredUser? FindById(int id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public StoredUser? TryCreate(string email, string passwordHash, string role)
    {
        lock (_sync)
        {
            if (_byEmail.ContainsKey(email)) return null;
            var stored = new StoredUser(_nextId++, email, passwordHash, role, DateTimeOffset.UtcNow);
            _byId.Add(stored.Id, stored);
            _byEmail.Add(email, stored.Id);
            return stored;
        }
    }

    public IReadOnlyList<StoredUser> GetAll()
    {
        lock (_sync)
            return _byId.Values.OrderBy(user => user.Id).ToArray();
    }

    public bool Delete(int id)
    {
        lock (_sync)
        {
            if (!_byId.Remove(id, out var stored)) return false;
            _byEmail.Remove(stored.Email);
            return true;
        }
    }
}
