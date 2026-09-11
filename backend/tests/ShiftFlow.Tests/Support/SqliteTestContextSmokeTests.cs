using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.SupportTests;

/// <summary>
/// The SQLite harness must build the schema from the real model and enforce the
/// relational constraints these use-case tests lean on — unlike EF Core InMemory
/// (CLAUDE.md §8).
/// </summary>
public class SqliteTestContextSmokeTests
{
    [Fact]
    public void Unique_project_name_per_supervisor_is_enforced()
    {
        using var ctx = new SqliteTestContext();

        var supervisor = ctx.Db.AddSupervisor("Acme");
        ctx.Db.AddProject(supervisor.Id, "Support");

        Assert.ThrowsAny<Exception>(() => ctx.Db.AddProject(supervisor.Id, "Support"));
    }

    [Fact]
    public void Composite_key_makes_a_duplicate_call_agent_project_assignment_fail()
    {
        using var ctx = new SqliteTestContext();

        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);

        Assert.ThrowsAny<Exception>(() => ctx.Db.Assign(callAgent.Id, project.Id));
    }

    [Fact]
    public void A_shift_inserts_despite_sqlite_having_no_rowversion()
    {
        using var ctx = new SqliteTestContext();

        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");

        var shift = ctx.Db.AddShift(project.Id);

        Assert.True(shift.Id > 0);
    }
}
