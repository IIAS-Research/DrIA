using Microsoft.EntityFrameworkCore.Migrations;
using Server.Models;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class FixStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bibliographie",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "PredictionModelName",
                table: "Models");

            migrationBuilder.RenameColumn(
                name: "Results",
                table: "Models",
                newName: "Bibliography");

            migrationBuilder.AlterColumn<Model.ModelExplanation>(
                name: "Explanation",
                table: "Models",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(Model.ModelExplanation),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<byte[]>(
                name: "CreatedAt",
                table: "ModelFiles",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Bibliography",
                table: "Models",
                newName: "Results");

            migrationBuilder.AlterColumn<Model.ModelExplanation>(
                name: "Explanation",
                table: "Models",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(Model.ModelExplanation),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<Model.ModelBibliographie>(
                name: "Bibliographie",
                table: "Models",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "PredictionModelName",
                table: "Models",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<byte[]>(
                name: "CreatedAt",
                table: "ModelFiles",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
