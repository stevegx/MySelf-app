using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exercise_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "muscles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LatinName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsFront = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_muscles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    DefaultTrackingMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VariationGroupExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LicenseShortName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LicenseUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Attribution = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exercises_exercise_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "exercise_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_equipment",
                columns: table => new
                {
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_equipment", x => new { x.ExerciseId, x.EquipmentId });
                    table.ForeignKey(
                        name: "FK_exercise_equipment_equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_equipment_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercise_muscles",
                columns: table => new
                {
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuscleId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_muscles", x => new { x.ExerciseId, x.MuscleId });
                    table.ForeignKey(
                        name: "FK_exercise_muscles_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exercise_muscles_muscles_MuscleId",
                        column: x => x.MuscleId,
                        principalTable: "muscles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_equipment_EquipmentId",
                table: "exercise_equipment",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_muscles_MuscleId",
                table: "exercise_muscles",
                column: "MuscleId");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_CategoryId",
                table: "exercises",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_Name",
                table: "exercises",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_Source_ExternalId",
                table: "exercises",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_equipment");

            migrationBuilder.DropTable(
                name: "exercise_muscles");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "muscles");

            migrationBuilder.DropTable(
                name: "exercise_categories");
        }
    }
}
