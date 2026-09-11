using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// The one mutation a supervisor may make to an existing shift: correcting the
/// time of a shift that is still Open and has no applications. A closed shift, a
/// shift with applications, and a stale RowVersion each fail with 409; another
/// supervisor's shift is 404.
/// </summary>
public class UpdateShiftTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, StubCurrentUser.Supervisor(userId, supervisorId), new TestClock());

    private static UpdateShiftRequest Move(int day, string rowVersion) => new()
    {
        StartUtc = At(day, 9),
        EndUtc = At(day, 17),
        RowVersion = rowVersion,
    };

    [Fact]
    public async Task Correcting_an_open_unapplied_shift_moves_its_window()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));
        var svc = ServiceFor(ctx, acme.UserId, acme.Id);

        var current = await svc.GetAsync(shift.Id, CancellationToken.None);
        var updated = await svc.UpdateAsync(shift.Id, Move(1, current.RowVersion), CancellationToken.None);

        Assert.Equal(At(1, 9), updated.StartUtc);
        Assert.Equal(At(1, 17), updated.EndUtc);

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(At(1, 9), stored.StartUtc);
        Assert.Equal(At(1, 17), stored.EndUtc);
    }

    [Fact]
    public async Task A_stale_RowVersion_is_a_ConcurrencyConflict()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));
        var svc = ServiceFor(ctx, acme.UserId, acme.Id);

        var stale = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            svc.UpdateAsync(shift.Id, Move(1, stale), CancellationToken.None));

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(At(1, 8), stored.StartUtc);
    }

    [Fact]
    public async Task Editing_a_closed_shift_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16), ShiftStatus.Closed);
        var svc = ServiceFor(ctx, acme.UserId, acme.Id);
        var current = await svc.GetAsync(shift.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            svc.UpdateAsync(shift.Id, Move(1, current.RowVersion), CancellationToken.None));
    }

    [Fact]
    public async Task Editing_a_shift_that_already_has_applications_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending);

        var svc = ServiceFor(ctx, acme.UserId, acme.Id);
        var current = await svc.GetAsync(shift.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            svc.UpdateAsync(shift.Id, Move(1, current.RowVersion), CancellationToken.None));

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(At(1, 8), stored.StartUtc);
    }

    [Fact]
    public async Task Editing_another_supervisors_shift_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var globexShift = ctx.Db.AddShift(globexProject.Id, At(1, 8), At(1, 16));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).UpdateAsync(
                globexShift.Id,
                Move(1, Convert.ToBase64String(new byte[8])),
                CancellationToken.None));
    }

    [Fact]
    public async Task An_end_before_start_correction_is_a_ValidationException()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));
        var svc = ServiceFor(ctx, acme.UserId, acme.Id);
        var current = await svc.GetAsync(shift.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.UpdateAsync(
                shift.Id,
                new UpdateShiftRequest { StartUtc = At(1, 16), EndUtc = At(1, 8), RowVersion = current.RowVersion },
                CancellationToken.None));
    }
}
