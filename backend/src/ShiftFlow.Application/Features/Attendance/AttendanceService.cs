using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.Attendance.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Attendance;

public sealed class AttendanceService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessScope _accessScope;
    private readonly IClock _clock;
    private readonly AttendanceOptions _options;

    public AttendanceService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IAccessScope accessScope,
        IClock clock,
        IOptions<AttendanceOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _accessScope = accessScope;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>Starts or refreshes the current shift's session after a successful login.</summary>
    public async Task OpenForLoginAsync(int callAgentId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var shift = await _db.Shifts
            .Where(s => s.StartUtc <= now && s.EndUtc > now)
            .Where(s => s.AssignedCallAgentId == callAgentId
                        || s.ShiftApplications.Any(a =>
                            a.CallAgentId == callAgentId && a.Status == ApplicationStatus.Approved))
            .OrderBy(s => s.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (shift is null)
        {
            return;
        }

        var session = await _db.AttendanceSessions
            .Where(s => s.CallAgentId == callAgentId
                        && s.ShiftId == shift.Id
                        && s.EndedAtUtc == null)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            _db.AttendanceSessions.Add(new AttendanceSession
            {
                CallAgentId = callAgentId,
                ShiftId = shift.Id,
                StartedAtUtc = now,
                LastSeenUtc = now,
            });
        }
        else
        {
            session.LastSeenUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        var session = await _db.AttendanceSessions
            .Where(s => s.CallAgentId == callAgentId && s.EndedAtUtc == null)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return;
        }

        session.LastSeenUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        var sessions = await _db.AttendanceSessions
            .Where(s => s.CallAgentId == callAgentId && s.EndedAtUtc == null)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        foreach (var session in sessions)
        {
            session.EndedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ActiveAttendanceResponse> GetActiveAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var freshSince = now.AddSeconds(-_options.StalenessSeconds);

        var query = _accessScope.RestrictToOwnSupervisor(
            _db.AttendanceSessions
                .AsNoTracking()
                .Where(a => a.EndedAtUtc == null
                            && a.LastSeenUtc >= freshSince
                            && a.Shift.StartUtc <= now
                            && a.Shift.EndUtc > now),
            a => a.Shift.Project.SupervisorId);

        var rows = await query
            .OrderBy(a => a.CallAgent.FullName)
            .ThenBy(a => a.CallAgentId)
            .Select(a => new ActiveAgentResponse(
                a.CallAgentId,
                a.CallAgent.FullName,
                a.ShiftId,
                a.Shift.ProjectId,
                a.Shift.Project.Name,
                a.StartedAtUtc,
                a.LastSeenUtc))
            .ToListAsync(cancellationToken);

        var agents = rows
            .GroupBy(a => a.CallAgentId)
            .Select(g => g.OrderByDescending(a => a.LastSeenUtc).First())
            .OrderBy(a => a.FullName)
            .ThenBy(a => a.CallAgentId)
            .ToList();

        return new ActiveAttendanceResponse(agents.Count, agents);
    }
}
