using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// A supervisor creates shifts on their own projects only. A shift for another
/// supervisor's project — or an unknown project — fails as NotFound and writes
/// nothing (CLAUDE.md §7).
/// </summary>
public class CreateShiftTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, StubCurrentUser.Supervisor(userId, supervisorId), new TestClock(Now));

    [Fact]
    public async Task Creating_a_shift_on_an_own_project_stores_it_Open()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        var created = await ServiceFor(ctx, acme.UserId, acme.Id).CreateAsync(
            new CreateShiftRequest { ProjectId = project.Id, StartUtc = At(1, 8), EndUtc = At(1, 16) },
            CancellationToken.None);

        Assert.Equal("Open", created.Status);
        Assert.Equal(At(1, 8), created.StartUtc);
        Assert.Equal(Now, created.CreatedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(created.RowVersion));

        var stored = await ctx.NewContext().Shifts.SingleAsync();
        Assert.Equal(project.Id, stored.ProjectId);
        Assert.Equal(ShiftStatus.Open, stored.Status);
    }

    [Fact]
    public async Task Creating_a_shift_for_another_supervisors_project_is_NotFound_and_writes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).CreateAsync(
                new CreateShiftRequest { ProjectId = globexProject.Id, StartUtc = At(1, 8), EndUtc = At(1, 16) },
                CancellationToken.None));

        Assert.False(await ctx.NewContext().Shifts.AnyAsync());
    }

    [Fact]
    public async Task Creating_a_shift_for_an_unknown_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).CreateAsync(
                new CreateShiftRequest { ProjectId = 9999, StartUtc = At(1, 8), EndUtc = At(1, 16) },
                CancellationToken.None));
    }

    [Fact]
    public async Task Creating_a_shift_that_ends_before_it_starts_is_a_ValidationException()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).CreateAsync(
                new CreateShiftRequest { ProjectId = project.Id, StartUtc = At(1, 16), EndUtc = At(1, 8) },
                CancellationToken.None));
    }

    [Fact]
    public async Task Overnight_shift_needs_no_flag()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        var created = await ServiceFor(ctx, acme.UserId, acme.Id).CreateAsync(
            new CreateShiftRequest { ProjectId = project.Id, StartUtc = At(1, 22), EndUtc = At(2, 2) },
            CancellationToken.None);

        Assert.Equal(At(2, 2), created.EndUtc);
    }
}
