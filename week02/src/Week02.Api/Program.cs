using Week02.Api.Endpoints;
using Week02.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Wspólne, deterministyczne repozytoria — bez bazy danych i paczek NuGet.
builder.Services.AddSingleton<IUserDirectory, SeededUserDirectory>();
builder.Services.AddSingleton<ITaskCatalog, SeededTaskCatalog>();
builder.Services.AddSingleton<IProductCatalog, SeededProductCatalog>();

var app = builder.Build();

app.MapUserEndpoints();
app.MapTaskEndpoints();
app.MapProductEndpoints();

app.Run();

public partial class Program { }
