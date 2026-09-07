using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_meals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_meal_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ServingBasis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ServingSizeGrams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    BasisKcal = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    BasisProteinG = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    BasisCarbG = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    BasisFatG = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    DefaultAmount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_meal_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_meal_items_saved_meals_SavedMealId",
                        column: x => x.SavedMealId,
                        principalTable: "saved_meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_meal_items_SavedMealId_SortOrder",
                table: "saved_meal_items",
                columns: new[] { "SavedMealId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_saved_meals_UserId_ArchivedAt_Name",
                table: "saved_meals",
                columns: new[] { "UserId", "ArchivedAt", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_meal_items");

            migrationBuilder.DropTable(
                name: "saved_meals");
        }
    }
}
