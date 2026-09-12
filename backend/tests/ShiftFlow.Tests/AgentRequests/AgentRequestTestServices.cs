using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.AgentRequests;
using ShiftFlow.Application.Features.Ratings;
using ShiftFlow.Infrastructure;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

internal static class AgentRequestTestServices
{
    internal static readonly DateTime Now =
        new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static AgentRequestService Create(
        SqliteTestContext ctx,
        ICurrentUser user,
        DateTime? now = null,
        decimal cap = 8m) =>
        new(
            ctx.Db,
            user,
            new AccessScope(user),
            new TestClock(now ?? Now),
            new PersianLeaveYear(),
            Options.Create(new RatingOptions { DowntimeCapHours = cap }));
}
