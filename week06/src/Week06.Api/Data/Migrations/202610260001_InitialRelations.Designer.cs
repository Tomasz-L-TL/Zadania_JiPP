using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Week06.Api.Data.Migrations;

public partial class InitialRelations
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        InitialSchemaModel.Build(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
