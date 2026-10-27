using Week03.Api.Endpoints;
using Week03.Api.Repositories;
using Week03.Api.Services;
using Week03.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IRequestValidator, DataAnnotationsRequestValidator>();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
builder.Services.AddSingleton<UserRegistrationService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapUserEndpoints();
app.MapTaskEndpoints();

app.Run();

public partial class Program { }
