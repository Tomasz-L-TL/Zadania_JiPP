using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Week06.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
public partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        InitialSchemaModel.Build(modelBuilder);
    }
}
