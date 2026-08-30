using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "food_cache_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Brand = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EnergyKcalPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    ProteinPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    CarbsPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    FatPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    SaturatedFatPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    SugarsPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    FiberPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    SaltPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    SodiumPer100g = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    ServingSizeRaw = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ServingQuantityGrams = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    PackageQuantityRaw = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SourceLastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_cache_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_food_cache_entries_Source_Barcode",
                table: "food_cache_entries",
                columns: new[] { "Source", "Barcode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_cache_entries");
        }
    }
}
