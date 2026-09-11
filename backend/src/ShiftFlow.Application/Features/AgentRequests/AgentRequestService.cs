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

        ValidateWindow(input.Type, input.StartUtc, input.EndUtc, shift, _clock.UtcNow);

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

    public async Task<AgentRequestResponse> ApproveAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var request = await FindForSupervisorAsync(id, cancellationToken);
        EnsurePending(request);
        EnsureStillCommitted(request);
        ValidateWindow(request.RequestType, request.StartUtc, request.EndUtc, request.Shift, _clock.UtcNow);

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
        catch (DbUpdateException exception)
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
            .Where(s =>
                (s.Status == ShiftStatus.Assigned && s.AssignedCallAgentId == callAgentId)
                || (s.Status == ShiftStatus.Closed && s.ShiftApplications.Any(a =>
                    a.CallAgentId == callAgentId && a.Status == ApplicationStatus.Approved)))
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
        var directlyAssigned = request.Shift.Status == ShiftStatus.Assigned
                               && request.Shift.AssignedCallAgentId == request.CallAgentId;
        var approvedApplication = request.Shift.Status == ShiftStatus.Closed
                                  && request.Shift.ShiftApplications.Any(a =>
                                      a.CallAgentId == request.CallAgentId
                                      && a.Status == ApplicationStatus.Approved);
        if (!directlyAssigned && !approvedApplication)
        {
            throw new BusinessRuleViolationException(
                "The shift is no longer committed to this CallAgent.");
        }
    }

    private static void ValidateWindow(
        AgentRequestType type,
        DateTime startUtc,
        DateTime endUtc,
        Shift shift,
        DateTime nowUtc)
    {
        if (startUtc < shift.StartUtc || endUtc > shift.EndUtc)
        {
            throw new ValidationException("The request interval must be inside the shift window.");
        }

        if (type == AgentRequestType.Leave
            && (startUtc != shift.StartUtc || endUtc != shift.EndUtc))
        {
            throw new ValidationException("Leave must cover the whole committed shift.");
        }

        if (type == AgentRequestType.Downtime && nowUtc < shift.StartUtc)
        {
            throw new ValidationException("Downtime can only be requested for a shift in progress or ended.");
        }
    }

    private async Task<int> RemainingLeaveDaysAsync(int callAgentId, CancellationToken cancellationToken)
    {
        var allowance = await _db.CallAgents
            .Where(a => a.Id == callAgentId)
            .Select(a => a.AnnualLeaveDays)
            .SingleAsync(cancellationToken);
        var year = _leaveYear.Resolve(_clock.UtcNow);
        var used = await _db.AgentRequests.CountAsync(r =>
            r.CallAgentId == callAgentId
            && r.RequestType == AgentRequestType.Leave
            && r.Status == AgentRequestStatus.Approved
            && r.StartUtc >= year.StartUtc
            && r.StartUtc < year.EndUtc,
            cancellationToken);
        return Math.Max(allowance - used, 0);
    }

    private async Task GuardDowntimeCapAsync(AgentRequest candidate, CancellationToken cancellationToken)
    {
        var approved = await _db.AgentRequests
            .AsNoTracking()
            .Where(r => r.CallAgentId == candidate.CallAgentId
                        && r.RequestType == AgentRequestType.Downtime
                        && r.Status == AgentRequestStatus.Approved)
            .Select(r => new { r.StartUtc, r.EndUtc })
            .ToListAsync(cancellationToken);

        var month = new DateTime(candidate.StartUtc.Year, candidate.StartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (month < candidate.EndUtc)
        {
            var nextMonth = month.AddMonths(1);
            var existingTicks = approved.Sum(r => OverlapTicks(r.StartUtc, r.EndUtc, month, nextMonth));
            var candidateTicks = OverlapTicks(candidate.StartUtc, candidate.EndUtc, month, nextMonth);
            var totalHours = (existingTicks + candidateTicks) / (decimal)TimeSpan.TicksPerHour;
            if (totalHours > _options.DowntimeCapHours)
            {
                throw new BusinessRuleViolationException(
                    $"Approved downtime cannot exceed {_options.DowntimeCapHours:0.##} hours per month.");
            }

            month = nextMonth;
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
        CancellationToken cancellationToken) =>
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
            await RemainingLeaveDaysAsync(request.CallAgentId, cancellationToken));
}
