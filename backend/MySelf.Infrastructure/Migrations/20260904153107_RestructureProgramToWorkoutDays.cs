using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureProgramToWorkoutDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_set_prescriptions_variant_exercises_VariantExerciseId",
                table: "set_prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_superset_groups_workout_variants_VariantId",
                table: "superset_groups");

            migrationBuilder.DropTable(
                name: "variant_exercises");

            migrationBuilder.DropTable(
                name: "workout_variants");

            migrationBuilder.DropTable(
                name: "workout_groups");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "workout_sessions");

            migrationBuilder.RenameColumn(
                name: "VariantName",
                table: "workout_sessions",
                newName: "DayName");

            migrationBuilder.RenameColumn(
                name: "SourceVariantId",
                table: "workout_sessions",
                newName: "SourceDayId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "superset_groups",
                newName: "DayId");

            migrationBuilder.RenameIndex(
                name: "IX_superset_groups_VariantId",
                table: "superset_groups",
                newName: "IX_superset_groups_DayId");

            migrationBuilder.RenameColumn(
                name: "VariantExerciseId",
                table: "set_prescriptions",
                newName: "DayExerciseId");

            migrationBuilder.RenameIndex(
                name: "IX_set_prescriptions_VariantExerciseId_SortOrder",
                table: "set_prescriptions",
                newName: "IX_set_prescriptions_DayExerciseId_SortOrder");

            migrationBuilder.CreateTable(
                name: "workout_days",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workout_days_workout_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "workout_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "day_exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SupersetGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersetMemberOrder = table.Column<int>(type: "integer", nullable: false),
                    RestSeconds = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_exercises_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_day_exercises_superset_groups_SupersetGroupId",
                        column: x => x.SupersetGroupId,
                        principalTable: "superset_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_day_exercises_workout_days_DayId",
                        column: x => x.DayId,
                        principalTable: "workout_days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_day_exercises_DayId_SortOrder",
                table: "day_exercises",
                columns: new[] { "DayId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_day_exercises_ExerciseId",
                table: "day_exercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_day_exercises_SupersetGroupId",
                table: "day_exercises",
                column: "SupersetGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_workout_days_ProgramId_SortOrder",
                table: "workout_days",
                columns: new[] { "ProgramId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_set_prescriptions_day_exercises_DayExerciseId",
                table: "set_prescriptions",
                column: "DayExerciseId",
                principalTable: "day_exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_superset_groups_workout_days_DayId",
                table: "superset_groups",
                column: "DayId",
                principalTable: "workout_days",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_set_prescriptions_day_exercises_DayExerciseId",
                table: "set_prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_superset_groups_workout_days_DayId",
                table: "superset_groups");

            migrationBuilder.DropTable(
                name: "day_exercises");

            migrationBuilder.DropTable(
                name: "workout_days");

            migrationBuilder.RenameColumn(
                name: "SourceDayId",
                table: "workout_sessions",
                newName: "SourceVariantId");

            migrationBuilder.RenameColumn(
                name: "DayName",
                table: "workout_sessions",
                newName: "VariantName");

            migrationBuilder.RenameColumn(
                name: "DayId",
                table: "superset_groups",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_superset_groups_DayId",
                table: "superset_groups",
                newName: "IX_superset_groups_VariantId");

            migrationBuilder.RenameColumn(
                name: "DayExerciseId",
                table: "set_prescriptions",
                newName: "VariantExerciseId");

            migrationBuilder.RenameIndex(
                name: "IX_set_prescriptions_DayExerciseId_SortOrder",
                table: "set_prescriptions",
                newName: "IX_set_prescriptions_VariantExerciseId_SortOrder");

            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "workout_sessions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workout_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workout_groups_workout_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "workout_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workout_variants_workout_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "workout_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variant_exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupersetGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RestSeconds = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SupersetMemberOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variant_exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_variant_exercises_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_variant_exercises_superset_groups_SupersetGroupId",
                        column: x => x.SupersetGroupId,
                        principalTable: "superset_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_variant_exercises_workout_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "workout_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_variant_exercises_ExerciseId",
                table: "variant_exercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_variant_exercises_SupersetGroupId",
                table: "variant_exercises",
                column: "SupersetGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_variant_exercises_VariantId_SortOrder",
                table: "variant_exercises",
                columns: new[] { "VariantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_workout_groups_ProgramId_SortOrder",
                table: "workout_groups",
                columns: new[] { "ProgramId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_workout_variants_GroupId_SortOrder",
                table: "workout_variants",
                columns: new[] { "GroupId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_set_prescriptions_variant_exercises_VariantExerciseId",
                table: "set_prescriptions",
                column: "VariantExerciseId",
                principalTable: "variant_exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_superset_groups_workout_variants_VariantId",
                table: "superset_groups",
                column: "VariantId",
                principalTable: "workout_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
