using Week01.Api.Notes;

namespace Week01.Api.Contracts;

public sealed record HelloResponse(string Message);

public sealed record HealthResponse(
    string Status,
    DateTimeOffset TimestampUtc,
    string Version);

public sealed record PublicConfigResponse(
    string DisplayName,
    string SupportEmail,
    bool MaintenanceMode);

public sealed record CreateNoteRequest(string? Title, string? Content);

public sealed record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    DateTimeOffset CreatedAtUtc)
{
    public static NoteResponse FromNote(Note note) =>
        new(note.Id, note.Title, note.Content, note.CreatedAtUtc);
}
