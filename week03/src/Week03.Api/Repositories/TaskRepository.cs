using Week03.Api.Domain;

namespace Week03.Api.Repositories;

public interface ITaskRepository
{
    TaskItem Add(TaskItem candidate);
    TaskItem? GetById(int id);
    IReadOnlyList<TaskItem> GetAll();
}

public sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly Lock _gate = new();
    private readonly List<TaskItem> _tasks = [];
    private int _nextId = 1;

    public TaskItem Add(TaskItem candidate)
    {
        lock (_gate)
        {
            var saved = candidate with { Id = _nextId++ };
            _tasks.Add(saved);
            return saved;
        }
    }

    public TaskItem? GetById(int id)
    {
        lock (_gate)
        {
            return _tasks.FirstOrDefault(task => task.Id == id);
        }
    }

    public IReadOnlyList<TaskItem> GetAll()
    {
        lock (_gate)
        {
            return _tasks.ToArray();
        }
    }
}
