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
    [InlineData(typeof(Employer), "UserId")]           // Employers  -> Users
    [InlineData(typeof(Expert), "UserId")]             // Experts    -> Users
    [InlineData(typeof(Project), "EmployerId")]        // Projects   -> Employers
    [InlineData(typeof(Shift), "ProjectId")]           // Shifts     -> Projects
    [InlineData(typeof(Availability), "ExpertId")]     // Availabilities  -> Experts
    [InlineData(typeof(ExpertRating), "ExpertId")]     // ExpertRatings   -> Experts
    [InlineData(typeof(ExpertProject), "ExpertId")]    // ExpertProjects  -> Experts (path 1)
    [InlineData(typeof(ShiftApplication), "ShiftId")]  // ShiftApplications -> Shifts (path 1)
    [InlineData(typeof(Recommendation), "ShiftId")]    // Recommendations   -> Shifts (path 1)
    public void Employer_project_shift_path_cascades(Type entity, string fkProperty)
    {
        Assert.Equal("Cascade", DeleteBehaviorOf(Model(), entity, fkProperty));
    }

    [Theory]
    [InlineData(typeof(ExpertProject), "ProjectId")]        // second path into ExpertProjects
    [InlineData(typeof(ShiftApplication), "ExpertId")]      // second path into ShiftApplications
    [InlineData(typeof(ShiftApplication), "DecidedByUserId")] // decision audit must survive
    [InlineData(typeof(Recommendation), "ExpertId")]        // second path into Recommendations
    public void Every_expert_side_fk_is_no_action(Type entity, string fkProperty)
    {
        Assert.Equal("NoAction", DeleteBehaviorOf(Model(), entity, fkProperty));
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
