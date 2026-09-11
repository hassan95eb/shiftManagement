using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Abstractions;

/// <summary>
/// <see cref="AccessScope.RestrictToOwnSupervisor{T}"/> must parameterize the
/// caller's supervisor id, not inline it as a SQL literal — a bare
/// <c>Expression.Constant</c> renders as a literal, so the database would
/// compile and cache a separate query plan per supervisor id instead of
/// reusing one plan across every caller.
/// </summary>
public class AccessScopeTests
{
    [Fact]
    public void RestrictToOwnSupervisor_parameterizes_the_supervisor_id()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        var accessScope = new AccessScope(StubCurrentUser.Supervisor(acme.UserId, acme.Id));
        var query = accessScope.RestrictToOwnSupervisor(ctx.Db.Projects, p => p.SupervisorId);

        var sql = query.ToQueryString();

        Assert.Contains("@supervisorId", sql);
        Assert.DoesNotContain($"SupervisorId\" = {acme.Id}", sql);
    }
}
