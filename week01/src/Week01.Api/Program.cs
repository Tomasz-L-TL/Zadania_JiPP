using System.Reflection;
using Microsoft.Extensions.Options;
using Week01.Api.Configuration;
using Week01.Api.Contracts;
using Week01.Api.Notes;

var builder = WebApplication.CreateBuilder(args);

// Bind only explicitly public application settings. Never serialize IConfiguration.
builder.Services.Configure<PublicAppOptions>(
    builder.Configuration.GetSection(PublicAppOptions.SectionName));
builder.Services.AddSingleton<INoteRepository, InMemoryNoteRepository>();

var app = builder.Build();

// Exercise 4: all primary endpoints are grouped under a versioned prefix.
var api = app.MapGroup("/api/v1");

// Exercise 1: the legacy /hello path is retained as required by the task.
api.MapGet("/hello", Hello).WithName("GetHello").WithTags("Basics");
app.MapGet("/hello", Hello).ExcludeFromDescription();

// Exercise 2: UTC timestamp and application assembly version.
api.MapGet("/health", Health).WithName("GetHealth").WithTags("Basics");
app.MapGet("/health", Health).ExcludeFromDescription();

// Exercise 3: explicit allow-listed response DTO without private settings.
api.MapGet("/config", Config).WithName("GetPublicConfig").WithTags("Configuration");

// Exercise 5: note data lives only in RAM and disappears after an app restart.
var notesApi = api.MapGroup("/notes").WithTags("Notes");

notesApi.MapGet("", (INoteRepository repository) =>
    Results.Ok(repository.GetAll().Select(NoteResponse.FromNote)))
    .WithName("GetNotes");

notesApi.MapGet("/{id:guid}", (Guid id, INoteRepository repository) =>
{
    var note = repository.GetById(id);
    return note is null
        ? Results.NotFound()
        : Results.Ok(NoteResponse.FromNote(note));
}).WithName("GetNoteById");

notesApi.MapPost("", (CreateNoteRequest? request, INoteRepository repository) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["body"] = ["A JSON object is required."]
        });
    }

    var errors = new Dictionary<string, string[]>();
    var title = request.Title?.Trim();
    var content = request.Content?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(title))
        errors["title"] = ["Title is required."];
    else if (title.Length > 100)
        errors["title"] = ["Title cannot exceed 100 characters."];

    if (content.Length > 2000)
        errors["content"] = ["Content cannot exceed 2000 characters."];

    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var note = repository.Add(title!, content);
    return Results.Created($"/api/v1/notes/{note.Id}", NoteResponse.FromNote(note));
}).WithName("CreateNote");

notesApi.MapDelete("/{id:guid}", (Guid id, INoteRepository repository) =>
    repository.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithName("DeleteNote");

app.Run();

static IResult Hello() => Results.Ok(new HelloResponse("Hello from Week 01 Minimal API!"));

static IResult Health() => Results.Ok(new HealthResponse(
    "healthy",
    DateTimeOffset.UtcNow,
    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown"));

static IResult Config(IOptions<PublicAppOptions> settings)
{
    var publicOptions = settings.Value;
    return Results.Ok(new PublicConfigResponse(
        publicOptions.DisplayName,
        publicOptions.SupportEmail,
        publicOptions.MaintenanceMode));
}
