using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRestSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "exercise_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupersetRestAfterRoundSeconds",
                table: "exercise_logs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "exercise_logs");

            migrationBuilder.DropColumn(
                name: "SupersetRestAfterRoundSeconds",
                table: "exercise_logs");
        }
    }
}
