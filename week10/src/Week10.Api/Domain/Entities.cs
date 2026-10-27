namespace Week10.Api.Domain;

public enum WorkStatus { Todo, InProgress, Done }

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    public List<TaskItem> Tasks { get; set; } = [];
}

public sealed class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public WorkStatus Status { get; set; } = WorkStatus.Todo;
    public DateTime? DueAtUtc { get; set; }
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public List<TaskTag> TaskTags { get; set; } = [];
}

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public List<TaskTag> TaskTags { get; set; } = [];
}

public sealed class TaskTag
{
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
