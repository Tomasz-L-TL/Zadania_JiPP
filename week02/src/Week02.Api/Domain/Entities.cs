namespace Week02.Api.Domain;

public sealed record User(int Id, string Name, string Email);
public sealed record WorkTask(int Id, string Title, bool Completed, int AssigneeId);
public sealed record Product(int Id, string Name, decimal Price, string Currency, string Category, bool InStock);
