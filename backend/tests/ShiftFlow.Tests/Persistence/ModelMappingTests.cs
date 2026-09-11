using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Tests.Persistence;

/// <summary>
/// Guards the EF mapping against docs/01-erd-and-schema.md. These assertions are
/// about the model, not business rules, so they run against the real SQL Server
/// provider with no database and no migration — a plain build never executes
/// <c>OnModelCreating</c>, so a bad delete rule, a missing string length or a
/// dropped filtered index would otherwise only surface when the migration is
/// generated.
/// </summary>
public class ModelMappingTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=none;Database=none;Trusted_Connection=True;")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IModel Model()
    {
        using var ctx = NewContext();
        return ctx.Model;
    }

    private static string DeleteBehaviorOf(IModel model, Type entity, string fkProperty) =>
        model.FindEntityType(entity)!.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == fkProperty))
            .DeleteBehavior.ToString();

    [Fact]
    public void Model_builds_against_the_sql_server_provider()
    {
        var model = Model();

        Assert.NotNull(model.FindEntityType(typeof(Shift)));
        Assert.Equal(10, model.GetEntityTypes().Count(t => !t.IsOwned()));
    }

    [Theory]
    [InlineData(typeof(Supervisor), "UserId")]           // Supervisors  -> Users
    [InlineData(typeof(CallAgent), "UserId")]             // CallAgents    -> Users
    [InlineData(typeof(Project), "SupervisorId")]        // Projects   -> Supervisors
    [InlineData(typeof(Shift), "ProjectId")]           // Shifts     -> Projects
    [InlineData(typeof(Availability), "CallAgentId")]     // Availabilities  -> CallAgents
    [InlineData(typeof(Rating), "CallAgentId")]     // Ratings   -> CallAgents
    [InlineData(typeof(CallAgentProject), "CallAgentId")]    // CallAgentProjects  -> CallAgents (path 1)
    [InlineData(typeof(ShiftApplication), "ShiftId")]  // ShiftApplications -> Shifts (path 1)
    [InlineData(typeof(Recommendation), "ShiftId")]    // Recommendations   -> Shifts (path 1)
    public void Supervisor_project_shift_path_cascades(Type entity, string fkProperty)
    {
        Assert.Equal("Cascade", DeleteBehaviorOf(Model(), entity, fkProperty));
    }

    [Theory]
    [InlineData(typeof(CallAgentProject), "ProjectId")]        // second path into CallAgentProjects
    [InlineData(typeof(ShiftApplication), "CallAgentId")]      // second path into ShiftApplications
    [InlineData(typeof(ShiftApplication), "DecidedByUserId")] // decision audit must survive
    [InlineData(typeof(Recommendation), "CallAgentId")]        // second path into Recommendations
    [InlineData(typeof(Shift), "AssignedCallAgentId")]         // direct assignment (V3)
    public void Every_call_agent_side_fk_is_no_action(Type entity, string fkProperty)
    {
        Assert.Equal("NoAction", DeleteBehaviorOf(Model(), entity, fkProperty));
    }

    [Fact]
    public void AssignedCallAgentId_index_is_filtered_to_non_null()
    {
        var index = Model().FindEntityType(typeof(Shift))!
            .GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_Shifts_AssignedCallAgentId");

        Assert.Equal(new[] { "AssignedCallAgentId" }, index.Properties.Select(p => p.Name));
        Assert.Equal("[AssignedCallAgentId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void One_approved_application_per_shift_is_a_filtered_unique_index()
    {
        var index = Model().FindEntityType(typeof(ShiftApplication))!
            .GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_ShiftApplications_OneApproved");

        Assert.True(index.IsUnique);
        Assert.Equal(new[] { "ShiftId" }, index.Properties.Select(p => p.Name));
        Assert.Equal("[Status] = 'Approved'", index.GetFilter());
    }

    [Fact]
    public void Shift_row_version_is_a_concurrency_token()
    {
        var rowVersion = Model().FindEntityType(typeof(Shift))!.FindProperty("RowVersion")!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void No_string_column_is_unbounded()
    {
        var unbounded = Model().GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null)
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}")
            .ToArray();

        Assert.Empty(unbounded); // an unbounded string maps to nvarchar(max) and cannot be an index key
    }
}
