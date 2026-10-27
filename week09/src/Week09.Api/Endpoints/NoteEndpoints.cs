using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Week09.Api.Contracts;
using Week09.Api.Data;
using Week09.Api.Domain;

namespace Week09.Api.Endpoints;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        var notes = app.MapGroup("/api/notes").WithTags("Notes").RequireAuthorization();
        notes.MapGet("", List);
        notes.MapGet("/{id:guid}", Get);
        notes.MapPost("", Create);
        notes.MapPut("/{id:guid}", Update);
        notes.MapDelete("/{id:guid}", Delete);

        app.MapGet("/api/admin/stats", async (Week09DbContext db, CancellationToken ct) =>
            Results.Ok(new StatsResponse(
                await db.Users.CountAsync(ct), await db.Notes.CountAsync(ct))))
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
    }

    private static Guid Owner(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static async Task<IResult> List(ClaimsPrincipal principal, Week09DbContext db,
        CancellationToken ct)
    {
        var ownerId = Owner(principal);
        var result = await db.Notes.AsNoTracking().Where(note => note.OwnerId == ownerId)
            .OrderBy(note => note.CreatedAtUtc).ThenBy(note => note.Id)
            .Select(note => new NoteResponse(note.Id, note.Title, note.Body, note.CreatedAtUtc))
            .ToListAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal principal, Week09DbContext db,
        CancellationToken ct)
    {
        var ownerId = Owner(principal);
        var note = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, ct);
        return note is null ? Results.NotFound() : Results.Ok(NoteResponse.FromNote(note));
    }

    private static async Task<IResult> Create(NoteRequest request, ClaimsPrincipal principal,
        Week09DbContext db, CancellationToken ct)
    {
        var errors = RequestValidation.Check(request);
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["Tytuł jest wymagany."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var note = new Note
        {
            Title = title!, Body = request.Body?.Trim() ?? string.Empty,
            OwnerId = Owner(principal)
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/notes/{note.Id}", NoteResponse.FromNote(note));
    }

    private static async Task<IResult> Update(Guid id, NoteRequest request, ClaimsPrincipal principal,
        Week09DbContext db, CancellationToken ct)
    {
        var errors = RequestValidation.Check(request);
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["Tytuł jest wymagany."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var note = await db.Notes.FirstOrDefaultAsync(item => item.Id == id &&
            item.OwnerId == Owner(principal), ct);
        if (note is null) return Results.NotFound();

        note.Title = title!;
        note.Body = request.Body?.Trim() ?? string.Empty;
        await db.SaveChangesAsync(ct);
        return Results.Ok(NoteResponse.FromNote(note));
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal principal, Week09DbContext db,
        CancellationToken ct)
    {
        var note = await db.Notes.FirstOrDefaultAsync(item => item.Id == id &&
            item.OwnerId == Owner(principal), ct);
        if (note is null) return Results.NotFound();

        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
