using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnualLeaveDays",
                table: "CallAgents",
                type: "int",
                nullable: false,
                defaultValue: 26);

            migrationBuilder.CreateTable(
                name: "AgentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CallAgentId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRequests", x => x.Id);
                    table.CheckConstraint("CK_AgentRequests_Range", "[EndUtc] > [StartUtc]");
                    table.CheckConstraint("CK_AgentRequests_RequestType", "[RequestType] IN ('Leave', 'Downtime')");
                    table.CheckConstraint("CK_AgentRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_AgentRequests_CallAgents_CallAgentId",
                        column: x => x.CallAgentId,
                        principalTable: "CallAgents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AgentRequests_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AgentRequests_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRequests_CallAgent_Type_Status",
                table: "AgentRequests",
                columns: new[] { "CallAgentId", "RequestType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRequests_DecidedByUserId",
                table: "AgentRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_AgentRequests_OneApprovedLeave",
                table: "AgentRequests",
                column: "ShiftId",
                unique: true,
                filter: "[Status] = 'Approved' AND [RequestType] = 'Leave'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRequests");

            migrationBuilder.DropColumn(
                name: "AnnualLeaveDays",
                table: "CallAgents");
        }
    }
}
