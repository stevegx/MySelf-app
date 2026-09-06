using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySelf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionSourceProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProgramId",
                table: "workout_sessions",
                type: "uuid",
                nullable: true);

            // Backfill existing sessions: a session's program is the program its source day
            // belongs to. Sessions whose source day was already deleted (or that were ad-hoc)
            // stay null. New sessions get SourceProgramId set at start time.
            migrationBuilder.Sql(@"
                UPDATE ""workout_sessions"" s
                   SET ""SourceProgramId"" = d.""ProgramId""
                  FROM ""workout_days"" d
                 WHERE s.""SourceDayId"" = d.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_workout_sessions_SourceProgramId",
                table: "workout_sessions",
                column: "SourceProgramId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workout_sessions_SourceProgramId",
                table: "workout_sessions");

            migrationBuilder.DropColumn(
                name: "SourceProgramId",
                table: "workout_sessions");
        }
    }
}
