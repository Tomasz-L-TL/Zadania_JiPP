using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Week06.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202610260001_InitialRelations")]
public partial class InitialRelations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Tags",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Tags", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Tasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<int>(type: "INTEGER", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tasks", x => x.Id);
                table.ForeignKey("FK_Tasks_Users_UserId", x => x.UserId,
                    "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Comments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TaskItemId = table.Column<int>(type: "INTEGER", nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Comments", x => x.Id);
                table.ForeignKey("FK_Comments_Tasks_TaskItemId", x => x.TaskItemId,
                    "Tasks", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TaskTags",
            columns: table => new
            {
                TaskItemId = table.Column<int>(type: "INTEGER", nullable: false),
                TagId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskTags", x => new { x.TaskItemId, x.TagId });
                table.ForeignKey("FK_TaskTags_Tasks_TaskItemId", x => x.TaskItemId,
                    "Tasks", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TaskTags_Tags_TagId", x => x.TagId,
                    "Tags", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Tasks_UserId", "Tasks", "UserId");
        migrationBuilder.CreateIndex("IX_Comments_TaskItemId", "Comments", "TaskItemId");
        migrationBuilder.CreateIndex("IX_TaskTags_TagId", "TaskTags", "TagId");
        migrationBuilder.CreateIndex("IX_Tags_Name", "Tags", "Name", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Comments");
        migrationBuilder.DropTable("TaskTags");
        migrationBuilder.DropTable("Tasks");
        migrationBuilder.DropTable("Tags");
        migrationBuilder.DropTable("Users");
    }
}
