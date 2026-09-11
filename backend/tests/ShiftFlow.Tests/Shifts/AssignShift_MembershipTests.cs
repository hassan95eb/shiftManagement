using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// A direct assignment mirrors apply rule 2: the CallAgent must be a member of
/// the shift's project. Unlike the CallAgent-facing apply path, the shift's
/// existence is not secret from the Supervisor who owns it, so a non-member is
/// a 409 (this CallAgent cannot work here), not a 404 — and an unknown
/// CallAgent id is a plain 404.
/// </summary>
public class AssignShift_MembershipTests
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
    public async Task Assigning_a_CallAgent_who_is_not_a_project_member_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var outsider = ctx.Db.AddCallAgent("Outsider");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                shift.Id,
                Request(outsider.Id, Convert.ToBase64String(shift.RowVersion)),
                CancellationToken.None));

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Null(stored.AssignedCallAgentId);
    }

    [Fact]
    public async Task Assigning_an_unknown_CallAgent_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                shift.Id,
                Request(9999, Convert.ToBase64String(shift.RowVersion)),
                CancellationToken.None));
    }
}
