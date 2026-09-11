using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CallAgentId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_CallAgents_CallAgentId",
                        column: x => x.CallAgentId,
                        principalTable: "CallAgents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_CallAgent_StartedAtUtc",
                table: "AttendanceSessions",
                columns: new[] { "CallAgentId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_Open",
                table: "AttendanceSessions",
                columns: new[] { "CallAgentId", "ShiftId" },
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_ShiftId",
                table: "AttendanceSessions",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSessions");
        }
    }
}
