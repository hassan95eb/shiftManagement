using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "[Role] IN ('Manager', 'Supervisor', 'CallAgent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "[Role] IN ('Supervisor', 'CallAgent')");
        }
    }
}
