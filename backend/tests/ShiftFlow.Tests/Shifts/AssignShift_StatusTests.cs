using System;
using System.Threading;
using System.Threading.Tasks;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// docs/04-v2-prompts.md V3 acceptance: assigning a shift that is already
/// <c>Assigned</c> or <c>Closed</c> is refused with 409 — only <c>Open</c> and
/// <c>Released</c> accept a direct assignment.
/// </summary>
public class AssignShift_StatusTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftAssignmentService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)));

    private static AssignShiftRequest Request(int callAgentId, string rowVersion) => new()
    {
        CallAgentId = callAgentId,
        RowVersion = rowVersion,
    };

    [Theory]
    [InlineData(ShiftStatus.Assigned)]
    [InlineData(ShiftStatus.Closed)]
    public async Task Assigning_a_shift_in_an_ineligible_status_is_refused(ShiftStatus status)
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16), status);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                shift.Id,
                Request(jane.Id, Convert.ToBase64String(shift.RowVersion)),
                CancellationToken.None));
    }
}
