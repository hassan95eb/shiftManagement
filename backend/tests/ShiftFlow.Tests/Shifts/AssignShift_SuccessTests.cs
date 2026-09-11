using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// docs/04-v2-prompts.md V3: a Supervisor fills an Open or Released shift
/// directly, with no <c>ShiftApplications</c> row created either way.
/// </summary>
public class AssignShift_SuccessTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftAssignmentService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)));

    private static AssignShiftRequest Request(int callAgentId, string rowVersion) => new()
    {
        CallAgentId = callAgentId,
        RowVersion = rowVersion,
    };

    [Fact]
    public async Task Assigning_an_Open_shift_to_a_project_member_makes_it_Assigned()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));

        var result = await ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
            shift.Id,
            Request(jane.Id, Convert.ToBase64String(shift.RowVersion)),
            CancellationToken.None);

        Assert.Equal("Assigned", result.Status);
        Assert.Equal(jane.Id, result.AssignedCallAgentId);

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(ShiftStatus.Assigned, stored.Status);
        Assert.Equal(jane.Id, stored.AssignedCallAgentId);

        Assert.False(await ctx.NewContext().ShiftApplications.AnyAsync());
    }

    [Fact]
    public async Task Assigning_a_Released_shift_fills_it_and_returns_it_to_Assigned()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var john = ctx.Db.AddCallAgent("John Smith");
        ctx.Db.Assign(jane.Id, project.Id);
        ctx.Db.Assign(john.Id, project.Id);

        // Released here stands in for the outcome of a V5 leave approval on an
        // originally-Assigned shift; the direct-fill path (this prompt) does not
        // depend on how the shift got there.
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16), ShiftStatus.Released, jane.Id);

        var result = await ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
            shift.Id,
            Request(john.Id, Convert.ToBase64String(shift.RowVersion)),
            CancellationToken.None);

        Assert.Equal("Assigned", result.Status);
        Assert.Equal(john.Id, result.AssignedCallAgentId);
    }

    [Fact]
    public async Task Assigning_another_supervisors_shift_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, globexProject.Id);
        var shift = ctx.Db.AddShift(globexProject.Id, At(1, 8), At(1, 16));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                shift.Id,
                Request(jane.Id, Convert.ToBase64String(shift.RowVersion)),
                CancellationToken.None));
    }
}
