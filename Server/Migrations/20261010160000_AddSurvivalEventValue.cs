using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    public partial class AddSurvivalEventValue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SurvivalModelEventValue", table: "Projects", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "StatusSnapshotJson", table: "Processes", type: "jsonb", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReportJson", table: "Models", type: "jsonb", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AuditJson", table: "Models", type: "jsonb", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReportMarkdown", table: "Models", type: "text", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SurvivalModelEventValue", table: "Projects");
            migrationBuilder.DropColumn(name: "StatusSnapshotJson", table: "Processes");
            migrationBuilder.DropColumn(name: "ReportJson", table: "Models");
            migrationBuilder.DropColumn(name: "AuditJson", table: "Models");
            migrationBuilder.DropColumn(name: "ReportMarkdown", table: "Models");
        }
    }
}
