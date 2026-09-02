using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutProgramBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workout_programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SplitLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workout_programs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true)
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
                name: "superset_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RestAfterRoundSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_superset_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_superset_groups_workout_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "workout_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variant_exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SupersetGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersetMemberOrder = table.Column<int>(type: "integer", nullable: false),
                    RestSeconds = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "set_prescriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsAmrap = table.Column<bool>(type: "boolean", nullable: false),
                    TargetToFailure = table.Column<bool>(type: "boolean", nullable: false),
                    TargetRepsMin = table.Column<int>(type: "integer", nullable: true),
                    TargetRepsMax = table.Column<int>(type: "integer", nullable: true),
                    TargetWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    TargetRir = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_set_prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_set_prescriptions_variant_exercises_VariantExerciseId",
                        column: x => x.VariantExerciseId,
                        principalTable: "variant_exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_set_prescriptions_VariantExerciseId_SortOrder",
                table: "set_prescriptions",
                columns: new[] { "VariantExerciseId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_superset_groups_VariantId",
                table: "superset_groups",
                column: "VariantId");

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
                name: "IX_workout_programs_UserId_active",
                table: "workout_programs",
                column: "UserId",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_workout_variants_GroupId_SortOrder",
                table: "workout_variants",
                columns: new[] { "GroupId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "set_prescriptions");

            migrationBuilder.DropTable(
                name: "variant_exercises");

            migrationBuilder.DropTable(
                name: "superset_groups");

            migrationBuilder.DropTable(
                name: "workout_variants");

            migrationBuilder.DropTable(
                name: "workout_groups");

            migrationBuilder.DropTable(
                name: "workout_programs");
        }
    }
}
