using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Recommendations;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Recommendations;

/// <summary>
/// <c>GET /api/shifts/{id}/recommendations</c>. Read-only over rows the Python
/// script writes. Ordering is score descending, then the CLAUDE.md §5 tie-break:
/// fewer approved hours in the month of <c>Shift.StartUtc</c> first, then earlier
/// <c>AppliedAtUtc</c>. Scoped to the caller's own shifts (CLAUDE.md §7).
/// </summary>
public class ShiftRecommendationsTests
{
    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime JulyDay(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime AppliedOn(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static RecommendationService ServiceFor(SqliteTestContext ctx, int userId, int employerId) =>
        new(ctx.Db, StubCurrentUser.Employer(userId, employerId));

    [Fact]
    public async Task Ranking_is_score_desc_then_fewer_approved_hours_then_earlier_application()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var target = ctx.Db.AddShift(project.Id, At(8), At(16));

        var top = ctx.Db.AddExpert("Top Scorer");
        var earlyBird = ctx.Db.AddExpert("Early Bird");
        var lightLoad = ctx.Db.AddExpert("Light Load");
        var heavyLoad = ctx.Db.AddExpert("Heavy Load");
        foreach (var e in new[] { top, earlyBird, lightLoad, heavyLoad })
        {
            ctx.Db.Assign(e.Id, project.Id);
        }

        // Applications to the target shift — the source of the AppliedAtUtc tie-breaker.
        ctx.Db.AddApplication(target.Id, top.Id, ApplicationStatus.Pending, AppliedOn(5));
        ctx.Db.AddApplication(target.Id, earlyBird.Id, ApplicationStatus.Pending, AppliedOn(1));
        ctx.Db.AddApplication(target.Id, lightLoad.Id, ApplicationStatus.Pending, AppliedOn(5));
        ctx.Db.AddApplication(target.Id, heavyLoad.Id, ApplicationStatus.Pending, AppliedOn(5));

        // heavyLoad already has an approved 8h shift in the target's month (July).
        var julyShift = ctx.Db.AddShift(project.Id, JulyDay(3, 8), JulyDay(3, 16));
        ctx.Db.AddApplication(julyShift.Id, heavyLoad.Id, ApplicationStatus.Approved, AppliedOn(2));

        // top is strictly highest; the other three tie at 75.
        ctx.Db.AddRecommendation(target.Id, top.Id, 90.00m);
        ctx.Db.AddRecommendation(target.Id, earlyBird.Id, 75.00m);
        ctx.Db.AddRecommendation(target.Id, lightLoad.Id, 75.00m);
        ctx.Db.AddRecommendation(target.Id, heavyLoad.Id, 75.00m);

        var ranking = await ServiceFor(ctx, employer.UserId, employer.Id)
            .ListForShiftAsync(target.Id, CancellationToken.None);

        Assert.Equal(
            new[] { top.Id, earlyBird.Id, lightLoad.Id, heavyLoad.Id },
            ranking.Select(r => r.ExpertId).ToArray());
    }

    [Fact]
    public async Task Approved_hours_outside_the_shifts_month_do_not_count_toward_the_tie_break()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var target = ctx.Db.AddShift(project.Id, At(8), At(16)); // July 2026

        var june = ctx.Db.AddExpert("June Load");
        var clean = ctx.Db.AddExpert("Clean Slate");
        ctx.Db.Assign(june.Id, project.Id);
        ctx.Db.Assign(clean.Id, project.Id);

        ctx.Db.AddApplication(target.Id, june.Id, ApplicationStatus.Pending, AppliedOn(5));
        ctx.Db.AddApplication(target.Id, clean.Id, ApplicationStatus.Pending, AppliedOn(6));

        // june's approved shift is in June, not July — it must not weigh on the tie-break.
        var juneShift = ctx.Db.AddShift(
            project.Id,
            new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 16, 0, 0, DateTimeKind.Utc));
        ctx.Db.AddApplication(juneShift.Id, june.Id, ApplicationStatus.Approved, AppliedOn(1));

        ctx.Db.AddRecommendation(target.Id, june.Id, 80.00m);
        ctx.Db.AddRecommendation(target.Id, clean.Id, 80.00m);

        var ranking = await ServiceFor(ctx, employer.UserId, employer.Id)
            .ListForShiftAsync(target.Id, CancellationToken.None);

        // Equal score, equal (zero) July hours, so the earlier application wins.
        Assert.Equal(new[] { june.Id, clean.Id }, ranking.Select(r => r.ExpertId).ToArray());
    }

    [Fact]
    public async Task A_shift_with_no_recommendations_yet_returns_an_empty_list()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));

        var ranking = await ServiceFor(ctx, employer.UserId, employer.Id)
            .ListForShiftAsync(shift.Id, CancellationToken.None);

        Assert.Empty(ranking);
    }

    [Fact]
    public async Task Another_employers_shift_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var shift = ctx.Db.AddShift(globexProject.Id, At(8), At(16));
        var jane = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(jane.Id, globexProject.Id);
        ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending);
        ctx.Db.AddRecommendation(shift.Id, jane.Id, 80.00m);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).ListForShiftAsync(shift.Id, CancellationToken.None));
    }
}
