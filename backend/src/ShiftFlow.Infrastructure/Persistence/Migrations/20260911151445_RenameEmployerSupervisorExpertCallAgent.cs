using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmployerSupervisorExpertCallAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Employers -> Supervisors
            migrationBuilder.RenameTable(
                name: "Employers",
                newName: "Supervisors");

            migrationBuilder.RenameIndex(
                name: "PK_Employers",
                table: "Supervisors",
                newName: "PK_Supervisors");

            migrationBuilder.RenameIndex(
                name: "UQ_Employers_UserId",
                table: "Supervisors",
                newName: "UQ_Supervisors_UserId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Employers_Users_UserId', N'FK_Supervisors_Users_UserId', N'OBJECT';");

            // Experts -> CallAgents
            migrationBuilder.RenameTable(
                name: "Experts",
                newName: "CallAgents");

            migrationBuilder.RenameIndex(
                name: "PK_Experts",
                table: "CallAgents",
                newName: "PK_CallAgents");

            migrationBuilder.RenameIndex(
                name: "UQ_Experts_UserId",
                table: "CallAgents",
                newName: "UQ_CallAgents_UserId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Experts_Users_UserId', N'FK_CallAgents_Users_UserId', N'OBJECT';");

            // ExpertProjects -> CallAgentProjects
            migrationBuilder.RenameTable(
                name: "ExpertProjects",
                newName: "CallAgentProjects");

            migrationBuilder.RenameColumn(
                name: "ExpertId",
                table: "CallAgentProjects",
                newName: "CallAgentId");

            migrationBuilder.RenameIndex(
                name: "PK_ExpertProjects",
                table: "CallAgentProjects",
                newName: "PK_CallAgentProjects");

            migrationBuilder.RenameIndex(
                name: "IX_ExpertProjects_ProjectId",
                table: "CallAgentProjects",
                newName: "IX_CallAgentProjects_ProjectId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_ExpertProjects_Experts_ExpertId', N'FK_CallAgentProjects_CallAgents_CallAgentId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_ExpertProjects_Projects_ProjectId', N'FK_CallAgentProjects_Projects_ProjectId', N'OBJECT';");

            // ExpertRatings -> Ratings
            migrationBuilder.RenameTable(
                name: "ExpertRatings",
                newName: "Ratings");

            migrationBuilder.RenameColumn(
                name: "ExpertId",
                table: "Ratings",
                newName: "CallAgentId");

            migrationBuilder.RenameIndex(
                name: "PK_ExpertRatings",
                table: "Ratings",
                newName: "PK_Ratings");

            migrationBuilder.RenameIndex(
                name: "UQ_ExpertRatings_Expert_Period",
                table: "Ratings",
                newName: "UQ_Ratings_CallAgent_Period");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.CK_ExpertRatings_Score', N'CK_Ratings_Score', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_ExpertRatings_Experts_ExpertId', N'FK_Ratings_CallAgents_CallAgentId', N'OBJECT';");

            // CK_Users_Role: the allowed value list itself changes, so this one is a genuine
            // drop + recreate, not a pure rename. The stored values are data, not schema —
            // existing rows still say 'Employer'/'Expert' and must be rewritten too, before
            // the new constraint can be added.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.Sql("UPDATE [Users] SET [Role] = 'Supervisor' WHERE [Role] = 'Employer';");
            migrationBuilder.Sql("UPDATE [Users] SET [Role] = 'CallAgent' WHERE [Role] = 'Expert';");

            // Columns / indexes / FKs on tables that are not themselves renamed, but that
            // reference a renamed table or carry the old vocabulary in their own name.
            migrationBuilder.RenameColumn(
                name: "ExpertId",
                table: "ShiftApplications",
                newName: "CallAgentId");

            migrationBuilder.RenameIndex(
                name: "UQ_ShiftApplications_Shift_Expert",
                table: "ShiftApplications",
                newName: "UQ_ShiftApplications_Shift_CallAgent");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftApplications_Expert_Status",
                table: "ShiftApplications",
                newName: "IX_ShiftApplications_CallAgent_Status");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_ShiftApplications_Experts_ExpertId', N'FK_ShiftApplications_CallAgents_CallAgentId', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "ExpertId",
                table: "Recommendations",
                newName: "CallAgentId");

            migrationBuilder.RenameIndex(
                name: "UQ_Recommendations_Shift_Expert",
                table: "Recommendations",
                newName: "UQ_Recommendations_Shift_CallAgent");

            migrationBuilder.RenameIndex(
                name: "IX_Recommendations_ExpertId",
                table: "Recommendations",
                newName: "IX_Recommendations_CallAgentId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Recommendations_Experts_ExpertId', N'FK_Recommendations_CallAgents_CallAgentId', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "EmployerId",
                table: "Projects",
                newName: "SupervisorId");

            migrationBuilder.RenameIndex(
                name: "UQ_Projects_Employer_Name",
                table: "Projects",
                newName: "UQ_Projects_Supervisor_Name");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Projects_Employers_EmployerId', N'FK_Projects_Supervisors_SupervisorId', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "ExpertId",
                table: "Availabilities",
                newName: "CallAgentId");

            migrationBuilder.RenameIndex(
                name: "IX_Availabilities_Expert_Range",
                table: "Availabilities",
                newName: "IX_Availabilities_CallAgent_Range");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Availabilities_Experts_ExpertId', N'FK_Availabilities_CallAgents_CallAgentId', N'OBJECT';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "[Role] IN ('Supervisor', 'CallAgent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.Sql("UPDATE [Users] SET [Role] = 'Employer' WHERE [Role] = 'Supervisor';");
            migrationBuilder.Sql("UPDATE [Users] SET [Role] = 'Expert' WHERE [Role] = 'CallAgent';");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Availabilities_CallAgents_CallAgentId', N'FK_Availabilities_Experts_ExpertId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_Availabilities_CallAgent_Range",
                table: "Availabilities",
                newName: "IX_Availabilities_Expert_Range");

            migrationBuilder.RenameColumn(
                name: "CallAgentId",
                table: "Availabilities",
                newName: "ExpertId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Projects_Supervisors_SupervisorId', N'FK_Projects_Employers_EmployerId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "UQ_Projects_Supervisor_Name",
                table: "Projects",
                newName: "UQ_Projects_Employer_Name");

            migrationBuilder.RenameColumn(
                name: "SupervisorId",
                table: "Projects",
                newName: "EmployerId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Recommendations_CallAgents_CallAgentId', N'FK_Recommendations_Experts_ExpertId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_Recommendations_CallAgentId",
                table: "Recommendations",
                newName: "IX_Recommendations_ExpertId");

            migrationBuilder.RenameIndex(
                name: "UQ_Recommendations_Shift_CallAgent",
                table: "Recommendations",
                newName: "UQ_Recommendations_Shift_Expert");

            migrationBuilder.RenameColumn(
                name: "CallAgentId",
                table: "Recommendations",
                newName: "ExpertId");

            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_ShiftApplications_CallAgents_CallAgentId', N'FK_ShiftApplications_Experts_ExpertId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftApplications_CallAgent_Status",
                table: "ShiftApplications",
                newName: "IX_ShiftApplications_Expert_Status");

            migrationBuilder.RenameIndex(
                name: "UQ_ShiftApplications_Shift_CallAgent",
                table: "ShiftApplications",
                newName: "UQ_ShiftApplications_Shift_Expert");

            migrationBuilder.RenameColumn(
                name: "CallAgentId",
                table: "ShiftApplications",
                newName: "ExpertId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "[Role] IN ('Employer', 'Expert')");

            // Ratings -> ExpertRatings
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Ratings_CallAgents_CallAgentId', N'FK_ExpertRatings_Experts_ExpertId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.CK_Ratings_Score', N'CK_ExpertRatings_Score', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "UQ_Ratings_CallAgent_Period",
                table: "Ratings",
                newName: "UQ_ExpertRatings_Expert_Period");

            migrationBuilder.RenameIndex(
                name: "PK_Ratings",
                table: "Ratings",
                newName: "PK_ExpertRatings");

            migrationBuilder.RenameColumn(
                name: "CallAgentId",
                table: "Ratings",
                newName: "ExpertId");

            migrationBuilder.RenameTable(
                name: "Ratings",
                newName: "ExpertRatings");

            // CallAgentProjects -> ExpertProjects
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_CallAgentProjects_Projects_ProjectId', N'FK_ExpertProjects_Projects_ProjectId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_CallAgentProjects_CallAgents_CallAgentId', N'FK_ExpertProjects_Experts_ExpertId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_CallAgentProjects_ProjectId",
                table: "CallAgentProjects",
                newName: "IX_ExpertProjects_ProjectId");

            migrationBuilder.RenameIndex(
                name: "PK_CallAgentProjects",
                table: "CallAgentProjects",
                newName: "PK_ExpertProjects");

            migrationBuilder.RenameColumn(
                name: "CallAgentId",
                table: "CallAgentProjects",
                newName: "ExpertId");

            migrationBuilder.RenameTable(
                name: "CallAgentProjects",
                newName: "ExpertProjects");

            // CallAgents -> Experts
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_CallAgents_Users_UserId', N'FK_Experts_Users_UserId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "UQ_CallAgents_UserId",
                table: "CallAgents",
                newName: "UQ_Experts_UserId");

            migrationBuilder.RenameIndex(
                name: "PK_CallAgents",
                table: "CallAgents",
                newName: "PK_Experts");

            migrationBuilder.RenameTable(
                name: "CallAgents",
                newName: "Experts");

            // Supervisors -> Employers
            migrationBuilder.Sql("EXEC sp_rename N'dbo.FK_Supervisors_Users_UserId', N'FK_Employers_Users_UserId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "UQ_Supervisors_UserId",
                table: "Supervisors",
                newName: "UQ_Employers_UserId");

            migrationBuilder.RenameIndex(
                name: "PK_Supervisors",
                table: "Supervisors",
                newName: "PK_Employers");

            migrationBuilder.RenameTable(
                name: "Supervisors",
                newName: "Employers");
        }
    }
}
