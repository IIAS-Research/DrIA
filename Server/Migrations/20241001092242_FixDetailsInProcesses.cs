using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class FixDetailsInProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Statut",
                table: "Processes",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "Pipeline",
                table: "Processes",
                newName: "PipelineJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Processes",
                newName: "Statut");

            migrationBuilder.RenameColumn(
                name: "PipelineJson",
                table: "Processes",
                newName: "Pipeline");
        }
    }
}
