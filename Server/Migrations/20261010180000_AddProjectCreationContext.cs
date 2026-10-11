using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    public partial class AddProjectCreationContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreationContextJson", table: "Projects", type: "jsonb", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "EvaluationMode", table: "Projects", type: "text", nullable: false, defaultValue: "split");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreationContextJson", table: "Projects");
            migrationBuilder.DropColumn(name: "EvaluationMode", table: "Projects");
        }
    }
}
