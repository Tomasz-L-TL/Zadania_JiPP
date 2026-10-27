using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Week10.Api.Data;

#nullable disable

namespace Week10.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202610260001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("Users", columns: table => new
        {
            Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
            NormalizedUsername = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
            Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
            NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
            PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
            Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

        migrationBuilder.CreateTable("Tags", columns: table => new
        {
            Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
            NormalizedName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Tags", x => x.Id));

        migrationBuilder.CreateTable("Tasks", columns: table => new
        {
            Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
            Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
            Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
            DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
            OwnerId = table.Column<int>(type: "INTEGER", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Tasks", x => x.Id);
            table.ForeignKey("FK_Tasks_Users_OwnerId", x => x.OwnerId,
                "Users", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable("TaskTags", columns: table => new
        {
            TaskItemId = table.Column<int>(type: "INTEGER", nullable: false),
            TagId = table.Column<int>(type: "INTEGER", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_TaskTags", x => new { x.TaskItemId, x.TagId });
            table.ForeignKey("FK_TaskTags_Tasks_TaskItemId", x => x.TaskItemId,
                "Tasks", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_TaskTags_Tags_TagId", x => x.TagId,
                "Tags", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateIndex("IX_Users_NormalizedUsername", "Users", "NormalizedUsername", unique: true);
        migrationBuilder.CreateIndex("IX_Users_NormalizedEmail", "Users", "NormalizedEmail", unique: true);
        migrationBuilder.CreateIndex("IX_Tags_NormalizedName", "Tags", "NormalizedName", unique: true);
        migrationBuilder.CreateIndex("IX_Tasks_OwnerId_Status", "Tasks", new[] { "OwnerId", "Status" });
        migrationBuilder.CreateIndex("IX_Tasks_DueAtUtc", "Tasks", "DueAtUtc");
        migrationBuilder.CreateIndex("IX_TaskTags_TagId", "TaskTags", "TagId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TaskTags");
        migrationBuilder.DropTable("Tasks");
        migrationBuilder.DropTable("Tags");
        migrationBuilder.DropTable("Users");
    }
}
