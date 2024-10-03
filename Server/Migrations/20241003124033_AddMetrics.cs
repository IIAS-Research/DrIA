using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class AddMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectFiles_ProjectId",
                table: "ProjectFiles");

            migrationBuilder.AddColumn<object>(
                name: "Metrics",
                table: "Models",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<long>(
                name: "ProcessId",
                table: "Models",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_ProjectId",
                table: "ProjectFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Models_ProcessId",
                table: "Models",
                column: "ProcessId");

            migrationBuilder.AddForeignKey(
                name: "FK_Models_Processes_ProcessId",
                table: "Models",
                column: "ProcessId",
                principalTable: "Processes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Models_Processes_ProcessId",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFiles_ProjectId",
                table: "ProjectFiles");

            migrationBuilder.DropIndex(
                name: "IX_Models_ProcessId",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "Metrics",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "ProcessId",
                table: "Models");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_ProjectId",
                table: "ProjectFiles",
                column: "ProjectId",
                unique: true);
        }
    }
}
