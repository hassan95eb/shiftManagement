using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Infrastructure.Persistence;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Persistence;

/// <summary>
/// The scenario seed is reviewer-facing data, so the invariants that keep it from
/// contradicting the business rules are worth pinning: it forms a consistent
/// graph, every seeded application is one the apply rules would actually have
/// allowed, running it twice changes nothing, and the applicant pool comes with
/// its recommendation ranking.
/// </summary>
public sealed class SeedDataTests
{
    private static SeedData SeederFor(SqliteTestContext ctx) =>
        new(ctx.Db, new FakePasswordHasher(), NullLogger<SeedData>.Instance);

    [Fact]
    public async Task Seed_writes_the_expected_shape()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();

        var db = ctx.NewContext();

        Assert.Equal(2, await db.Supervisors.CountAsync());
        Assert.Equal(7, await db.CallAgents.CountAsync());
        Assert.Equal(10, await db.Users.CountAsync());
        Assert.Equal(3, await db.Projects.CountAsync());
        Assert.Equal(5, await db.Shifts.CountAsync());
        Assert.Equal(6, await db.ShiftApplications.CountAsync());
        Assert.Equal(3, await db.Recommendations.CountAsync());

        // The manager account exists, has no Supervisor/CallAgent profile, and is hashed.
        var manager = await db.Users.SingleAsync(u => u.Username == SeedData.ManagerUsername);
        Assert.Equal(UserRole.Manager, manager.Role);
        Assert.NotEqual(SeedData.DemoPassword, manager.PasswordHash);
        Assert.False(await db.Supervisors.AnyAsync(s => s.UserId == manager.Id));
        Assert.False(await db.CallAgents.AnyAsync(c => c.UserId == manager.Id));

        // Both supervisor accounts exist and are hashed, not stored in the clear.
        var supervisor = await db.Users.SingleAsync(u => u.Username == SeedData.SupervisorUsername);
        Assert.Equal(UserRole.Supervisor, supervisor.Role);
        Assert.NotEqual(SeedData.DemoPassword, supervisor.PasswordHash);
        Assert.True(await db.Users.AnyAsync(u => u.Username == SeedData.RivalSupervisorUsername));

        // One completed decision: a shift Closed with exactly one Approved row and
        // a sibling Rejected carrying the cascade note.
        var closedStartUtc = new System.DateTime(2026, 11, 11, 8, 0, 0, System.DateTimeKind.Utc);
        var closed = await db.Shifts.SingleAsync(s => s.Status == ShiftStatus.Closed && s.StartUtc == closedStartUtc);
        var decisions = await db.ShiftApplications.Where(a => a.ShiftId == closed.Id).ToListAsync();
        Assert.Equal(1, decisions.Count(a => a.Status == ApplicationStatus.Approved));
        var rejected = Assert.Single(decisions, a => a.Status == ApplicationStatus.Rejected);
        Assert.Equal("Shift filled by another CallAgent.", rejected.DecisionNote);
        Assert.All(decisions, a => Assert.NotNull(a.DecidedByUserId));
    }

    [Fact]
    public async Task Every_pending_application_is_one_the_apply_rules_would_have_allowed()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();

        var db = ctx.NewContext();
        var pending = await db.ShiftApplications
            .Where(a => a.Status == ApplicationStatus.Pending)
            .Include(a => a.Shift)
            .ToListAsync();

        Assert.NotEmpty(pending);
        foreach (var application in pending)
        {
            // Rule 2: the CallAgent is assigned to the shift's project.
            Assert.True(await db.CallAgentProjects.AnyAsync(ep =>
                ep.CallAgentId == application.CallAgentId && ep.ProjectId == application.Shift.ProjectId));

            // Rule 1: the shift is Open.
            Assert.Equal(ShiftStatus.Open, application.Shift.Status);

            // Rule 3: one availability window covers the whole shift.
            Assert.True(await db.Availabilities.AnyAsync(w =>
                w.CallAgentId == application.CallAgentId
                && w.StartUtc <= application.Shift.StartUtc
                && w.EndUtc >= application.Shift.EndUtc));

            // Rule 5: no overlapping shift the same CallAgent is already approved for.
            Assert.False(await db.ShiftApplications.AnyAsync(other =>
                other.CallAgentId == application.CallAgentId
                && other.Status == ApplicationStatus.Approved
                && other.Shift.StartUtc < application.Shift.EndUtc
                && other.Shift.EndUtc > application.Shift.StartUtc));
        }
    }

    [Fact]
    public async Task The_applicant_pool_shift_has_several_applicants_and_a_ranking()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();

        var db = ctx.NewContext();
        var pool = await db.Shifts
            .Where(s => s.Status == ShiftStatus.Open)
            .OrderBy(s => s.StartUtc)
            .FirstAsync();

        var applicants = await db.ShiftApplications.CountAsync(a => a.ShiftId == pool.Id);
        Assert.True(applicants >= 3, $"expected several pending applicants, got {applicants}");

        // A recommendation for every applicant, and the scores put Ada on top.
        var recs = await db.Recommendations
            .Where(r => r.ShiftId == pool.Id)
            .OrderByDescending(r => r.Score)
            .ToListAsync();
        Assert.Equal(applicants, recs.Count);
        Assert.All(recs, r => Assert.InRange(r.Score, 0m, 100m));
        Assert.Equal(recs.Max(r => r.Score), recs.First().Score);
    }

    [Fact]
    public async Task Previous_month_ratings_are_present_and_kite_has_none()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();

        var db = ctx.NewContext();

        // The pool shift starts in 2026-11, so 2026-10 is the "previous month".
        Assert.True(await db.Ratings.CountAsync(r => r.Period == "2026-10") >= 4);

        var kite = await db.CallAgents.SingleAsync(e => e.FullName == "Kite Tanaka");
        Assert.False(await db.Ratings.AnyAsync(r => r.CallAgentId == kite.Id));
    }

    [Fact]
    public async Task Seed_run_twice_does_not_duplicate()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();
        await SeederFor(ctx).SeedAsync();

        var db = ctx.NewContext();

        Assert.Equal(10, await db.Users.CountAsync());
        Assert.Equal(7, await db.CallAgents.CountAsync());
        Assert.Equal(3, await db.Projects.CountAsync());
        Assert.Equal(5, await db.Shifts.CountAsync());
        Assert.Equal(6, await db.ShiftApplications.CountAsync());
        Assert.Equal(3, await db.Recommendations.CountAsync());
        Assert.Equal(6, await db.Ratings.CountAsync());
    }
}
