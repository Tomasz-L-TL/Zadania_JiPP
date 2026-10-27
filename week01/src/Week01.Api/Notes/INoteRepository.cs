namespace Week01.Api.Notes;

public interface INoteRepository
{
    IReadOnlyList<Note> GetAll();
    Note? GetById(Guid id);
    Note Add(string title, string content);
    bool Delete(Guid id);
}

public sealed record Note(Guid Id, string Title, string Content, DateTimeOffset CreatedAtUtc);
