using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_Status",
                table: "Shifts");

            migrationBuilder.AddColumn<int>(
                name: "AssignedCallAgentId",
                table: "Shifts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_AssignedCallAgentId",
                table: "Shifts",
                column: "AssignedCallAgentId",
                filter: "[AssignedCallAgentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_Status",
                table: "Shifts",
                sql: "[Status] IN ('Open', 'Assigned', 'Released', 'Closed')");

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_CallAgents_AssignedCallAgentId",
                table: "Shifts",
                column: "AssignedCallAgentId",
                principalTable: "CallAgents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_CallAgents_AssignedCallAgentId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_AssignedCallAgentId",
                table: "Shifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_Status",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "AssignedCallAgentId",
                table: "Shifts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_Status",
                table: "Shifts",
                sql: "[Status] IN ('Open', 'Closed')");
        }
    }
}
