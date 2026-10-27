namespace Week06.Api.Domain;

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public sealed class TaskItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

public sealed class Comment
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
}

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
