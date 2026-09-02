using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGoalAndEstimateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    CalorieTarget = table.Column<int>(type: "integer", nullable: true),
                    ProteinGrams = table.Column<int>(type: "integer", nullable: true),
                    CarbGrams = table.Column<int>(type: "integer", nullable: true),
                    FatGrams = table.Column<int>(type: "integer", nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_goals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_estimate_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserGoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    HeightCm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AgeYears = table.Column<int>(type: "integer", nullable: false),
                    CalculationSex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActivityLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FormulaName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FormulaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bmr = table.Column<int>(type: "integer", nullable: false),
                    Tdee = table.Column<int>(type: "integer", nullable: false),
                    SelectedAdjustment = table.Column<int>(type: "integer", nullable: false),
                    SuggestedTarget = table.Column<int>(type: "integer", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_estimate_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nutrition_estimate_snapshots_user_goals_UserGoalId",
                        column: x => x.UserGoalId,
                        principalTable: "user_goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_estimate_snapshots_UserGoalId",
                table: "nutrition_estimate_snapshots",
                column: "UserGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_estimate_snapshots_UserId",
                table: "nutrition_estimate_snapshots",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_goals_UserId_EffectiveFrom",
                table: "user_goals",
                columns: new[] { "UserId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_estimate_snapshots");

            migrationBuilder.DropTable(
                name: "user_goals");
        }
    }
}
