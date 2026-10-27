using System.Collections.Concurrent;

namespace Week01.Api.Notes;

public sealed class InMemoryNoteRepository : INoteRepository
{
    private readonly ConcurrentDictionary<Guid, Note> _notes = new();

    public IReadOnlyList<Note> GetAll() => _notes.Values
        .OrderBy(note => note.CreatedAtUtc)
        .ThenBy(note => note.Id)
        .ToArray();

    public Note? GetById(Guid id) =>
        _notes.TryGetValue(id, out var note) ? note : null;

    public Note Add(string title, string content)
    {
        var note = new Note(Guid.NewGuid(), title, content, DateTimeOffset.UtcNow);
        if (!_notes.TryAdd(note.Id, note))
            throw new InvalidOperationException("Unable to allocate a unique note ID.");
        return note;
    }

    public bool Delete(Guid id) => _notes.TryRemove(id, out _);
}
