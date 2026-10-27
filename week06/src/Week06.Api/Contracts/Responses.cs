namespace Week06.Api.Contracts;

public sealed record TaskResponse(int Id, string Title, bool IsCompleted);
public sealed record UserTasksResponse(int Id, string Name, IReadOnlyList<TaskResponse> Tasks);

public sealed record CommentResponse(int Id, string Text);
public sealed record TaskDetailsResponse(int Id, string Title, bool IsCompleted,
    IReadOnlyList<CommentResponse> Comments);
public sealed record UserDetailsResponse(int Id, string Name,
    IReadOnlyList<TaskDetailsResponse> Tasks);

public sealed record TagResponse(int Id, string Name);
public sealed record TaskGraphResponse(int Id, string Title, bool IsCompleted,
    IReadOnlyList<CommentResponse> Comments, IReadOnlyList<TagResponse> Tags);
public sealed record UserGraphResponse(int Id, string Name,
    IReadOnlyList<TaskGraphResponse> Tasks);

public sealed record TaskLiteResponse(int Id, string Title, int CommentCount, int TagCount);
public sealed record UserSummaryResponse(int Id, string Name, int TaskCount,
    int CompletedTaskCount, IReadOnlyList<TaskLiteResponse> Tasks);
