using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Application.Features.AgentRequests.Validators;
using ShiftFlow.Application.Features.Ratings;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.AgentRequests;

public sealed class AgentRequestService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessScope _accessScope;
    private readonly IClock _clock;
    private readonly ILeaveYear _leaveYear;
    private readonly RatingOptions _options;

    public AgentRequestService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IAccessScope accessScope,
        IClock clock,
        ILeaveYear leaveYear,
        IOptions<RatingOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _accessScope = accessScope;
        _clock = clock;
        _leaveYear = leaveYear;
        _options = options.Value;
    }

    public async Task<AgentRequestResponse> CreateAsync(
        CreateAgentRequest request,
        CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        var input = AgentRequestValidator.ValidateAndNormalize(request);
        var shift = await FindCommittedShiftAsync(callAgentId, input.ShiftId, cancellationToken);

        if (WindowFailureReason(input.Type, input.StartUtc, input.EndUtc, shift, _clock.UtcNow) is { } failure)
        {
            throw new ValidationException(failure);
        }

        var entity = new AgentRequest
        {
            CallAgentId = callAgentId,
            ShiftId = shift.Id,
            RequestType = input.Type,
            RequestedAtUtc = _clock.UtcNow,
            StartUtc = input.StartUtc,
            EndUtc = input.EndUtc,
            Reason = input.Reason,
            Status = AgentRequestStatus.Pending,
        };
        _db.AgentRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentRequestResponse>> ListAsync(
        AgentRequestListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _db.AgentRequests.AsNoTracking();
        query = _currentUser.Role == UserRole.CallAgent
            ? query.Where(r => r.CallAgentId == _currentUser.RequireCallAgentId())
            : _accessScope.RestrictToOwnSupervisor(query, r => r.Shift.Project.SupervisorId);

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        var rows = await query
            .OrderBy(r => r.Status == AgentRequestStatus.Pending ? 0 : 1)
            .ThenBy(r => r.RequestedAtUtc)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var leaveAgentIds = rows
            .Where(r => r.RequestType == AgentRequestType.Leave)
            .Select(r => r.CallAgentId)
            .Distinct()
            .ToArray();
        var balances = await RemainingLeaveDaysAsync(leaveAgentIds, cancellationToken);

        return rows
            .Select(r => ToResponse(
                r,
                r.RequestType == AgentRequestType.Leave ? balances[r.CallAgentId] : null))
            .ToList();
    }

    public async Task<AgentRequestResponse> ApproveAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var request = await FindForSupervisorAsync(id, cancellationToken);
        EnsurePending(request);
        EnsureStillCommitted(request);
        if (WindowFailureReason(
                request.RequestType, request.StartUtc, request.EndUtc, request.Shift, _clock.UtcNow) is { } failure)
        {
            throw new BusinessRuleViolationException(failure);
        }

        if (request.RequestType == AgentRequestType.Leave)
        {
            var remaining = await RemainingLeaveDaysAsync(request.CallAgentId, cancellationToken);
            if (remaining <= 0)
            {
                throw new BusinessRuleViolationException("The CallAgent has no remaining leave balance.");
            }

            if (request.Shift.Status == ShiftStatus.Assigned)
            {
                request.Shift.Status = ShiftStatus.Released;
            }
        }
        else
        {
            await GuardDowntimeCapAsync(request, cancellationToken);
        }

        request.Status = AgentRequestStatus.Approved;
        request.DecidedByUserId = _currentUser.UserId;
        request.DecidedAtUtc = _clock.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException(
                "This request or shift was changed by another user. Reload it and try again.", exception);
        }
        catch (DbUpdateException exception) when (request.RequestType == AgentRequestType.Leave)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessRuleViolationException(
                "An approved leave request already exists for this shift.", exception);
        }

        return await ToResponseAsync(request, cancellationToken);
    }

    public async Task<AgentRequestResponse> RejectAsync(int id, CancellationToken cancellationToken)
    {
        var request = await FindForSupervisorAsync(id, cancellationToken);
        EnsurePending(request);
        request.Status = AgentRequestStatus.Rejected;
        request.DecidedByUserId = _currentUser.UserId;
        request.DecidedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(request, cancellationToken);
    }

    private async Task<Shift> FindCommittedShiftAsync(
        int callAgentId,
        int shiftId,
        CancellationToken cancellationToken) =>
        await _db.Shifts
            .AsNoTracking()
            .Where(s => s.Id == shiftId)
            .Where(ShiftCommitmentPolicy.ForCallAgent(callAgentId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Committed shift not found.");

    private async Task<AgentRequest> FindForSupervisorAsync(int id, CancellationToken cancellationToken) =>
        await _accessScope
            .RestrictToOwnSupervisor(
                _db.AgentRequests
                    .Include(r => r.Shift)
                    .ThenInclude(s => s.ShiftApplications)
                    .Include(r => r.CallAgent),
                r => r.Shift.Project.SupervisorId)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Agent request not found.");

    private static void EnsurePending(AgentRequest request)
    {
        if (request.Status != AgentRequestStatus.Pending)
        {
            throw new BusinessRuleViolationException("This request has already been decided.");
        }
    }

    private static void EnsureStillCommitted(AgentRequest request)
    {
        if (!ShiftCommitmentPolicy.IsCommittedTo(request.Shift, request.CallAgentId))
        {
            throw new BusinessRuleViolationException(
                "The shift is no longer committed to this CallAgent.");
        }
    }

    private static string? WindowFailureReason(
        AgentRequestType type,
        DateTime startUtc,
        DateTime endUtc,
        Shift shift,
        DateTime nowUtc)
    {
        if (startUtc < shift.StartUtc || endUtc > shift.EndUtc)
        {
            return "The request interval must be inside the shift window.";
        }

        if (type == AgentRequestType.Leave
            && (startUtc != shift.StartUtc || endUtc != shift.EndUtc))
        {
            return "Leave must cover the whole committed shift.";
        }

        if (type == AgentRequestType.Downtime && nowUtc < shift.StartUtc)
        {
            return "Downtime can only be requested for a shift in progress or ended.";
        }

        return null;
    }

    private async Task<int> RemainingLeaveDaysAsync(int callAgentId, CancellationToken cancellationToken)
    {
        var balances = await RemainingLeaveDaysAsync([callAgentId], cancellationToken);
        return balances[callAgentId];
    }

    private async Task<IReadOnlyDictionary<int, int>> RemainingLeaveDaysAsync(
        int[] callAgentIds,
        CancellationToken cancellationToken)
    {
        if (callAgentIds.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        var year = _leaveYear.Resolve(_clock.UtcNow);
        var rows = await _db.CallAgents
            .AsNoTracking()
            .Where(a => callAgentIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.AnnualLeaveDays,
                Used = a.AgentRequests.Count(r =>
                    r.RequestType == AgentRequestType.Leave
                    && r.Status == AgentRequestStatus.Approved
                    && r.StartUtc >= year.StartUtc
                    && r.StartUtc < year.EndUtc),
            })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => Math.Max(r.AnnualLeaveDays - r.Used, 0));
    }

    private async Task GuardDowntimeCapAsync(AgentRequest candidate, CancellationToken cancellationToken)
    {
        var months = new List<LeaveMonthRange>();
        var month = _leaveYear.ResolveMonth(candidate.StartUtc);
        while (month.StartUtc < candidate.EndUtc)
        {
            months.Add(month);
            month = _leaveYear.ResolveMonth(month.EndUtc);
        }

        var rangeStartUtc = months[0].StartUtc;
        var rangeEndUtc = months[^1].EndUtc;
        var approved = await _db.AgentRequests
            .AsNoTracking()
            .Where(r => r.CallAgentId == candidate.CallAgentId
                        && r.RequestType == AgentRequestType.Downtime
                        && r.Status == AgentRequestStatus.Approved
                        && r.EndUtc > rangeStartUtc
                        && r.StartUtc < rangeEndUtc)
            .Select(r => new { r.StartUtc, r.EndUtc })
            .ToListAsync(cancellationToken);

        foreach (var currentMonth in months)
        {
            var existingTicks = approved.Sum(r =>
                OverlapTicks(r.StartUtc, r.EndUtc, currentMonth.StartUtc, currentMonth.EndUtc));
            var candidateTicks = OverlapTicks(
                candidate.StartUtc, candidate.EndUtc, currentMonth.StartUtc, currentMonth.EndUtc);
            var totalHours = (existingTicks + candidateTicks) / (decimal)TimeSpan.TicksPerHour;
            if (totalHours > _options.DowntimeCapHours)
            {
                throw new BusinessRuleViolationException(
                    $"Approved downtime cannot exceed {_options.DowntimeCapHours:0.##} hours per month.");
            }
        }
    }

    private static long OverlapTicks(DateTime start, DateTime end, DateTime windowStart, DateTime windowEnd)
    {
        var overlapStart = start > windowStart ? start : windowStart;
        var overlapEnd = end < windowEnd ? end : windowEnd;
        return overlapEnd > overlapStart ? (overlapEnd - overlapStart).Ticks : 0;
    }

    private async Task<AgentRequestResponse> ToResponseAsync(
        AgentRequest request,
        CancellationToken cancellationToken)
    {
        int? remaining = request.RequestType == AgentRequestType.Leave
            ? await RemainingLeaveDaysAsync(request.CallAgentId, cancellationToken)
            : null;
        return ToResponse(request, remaining);
    }

    private static AgentRequestResponse ToResponse(AgentRequest request, int? remainingLeaveDays) =>
        new(
            request.Id,
            request.CallAgentId,
            request.ShiftId,
            request.RequestType.ToString(),
            request.RequestedAtUtc,
            request.StartUtc,
            request.EndUtc,
            request.Reason,
            request.Status.ToString(),
            request.DecidedByUserId,
            request.DecidedAtUtc,
            request.DecisionNote,
            remainingLeaveDays);
}
