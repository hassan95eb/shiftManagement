using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.CallAgents.Dtos;
using ShiftFlow.Application.Features.CallAgents.Validators;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.CallAgents;

/// <summary>
/// CallAgent use cases available to a supervisor. CallAgents are a shared specialist
/// pool — the schema has no supervisor-ownership column on <c>CallAgents</c> — so the
/// list and lookup are not supervisor-scoped; the ownership boundary that matters
/// is on the assignment side (<see cref="CallAgentProjectService"/>), which is
/// rooted on the project. All endpoints still require a Supervisor principal.
/// </summary>
public sealed class CallAgentService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly LeaveOptions _leaveOptions;

    public CallAgentService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        IClock clock,
        IOptions<LeaveOptions> leaveOptions)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _leaveOptions = leaveOptions.Value;
    }

    /// <summary>
    /// Creates the <c>Users</c> row (role CallAgent, hashed password) and the
    /// <c>CallAgents</c> row together. Both inserts go in one
    /// <see cref="IAppDbContext.SaveChangesAsync"/> call, so EF wraps them in a
    /// single transaction — a failure on either leaves neither behind.
    /// </summary>
    public async Task<CallAgentResponse> CreateAsync(CreateCallAgentRequest request, CancellationToken cancellationToken)
    {
        // Guarantees the caller is a provisioned supervisor before any write.
        _ = _currentUser.RequireSupervisorId();

        var (username, password, fullName) = CreateCallAgentRequestValidator.ValidateAndNormalize(request);

        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == username, cancellationToken);
        if (usernameTaken)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["username"] = ["This username is already taken."],
            });
        }

        var now = _clock.UtcNow;
        var callAgent = new CallAgent
        {
            FullName = fullName,
            IsActive = true,
            // The EF default remains 26 as the database-level mirror. New
            // application writes use the configured product default explicitly.
            AnnualLeaveDays = _leaveOptions.AnnualDaysDefault,
            CreatedAtUtc = now,
            User = new User
            {
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Role = UserRole.CallAgent,
                IsActive = true,
                CreatedAtUtc = now,
            },
        };

        _db.CallAgents.Add(callAgent);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(callAgent);
    }

    public async Task<IReadOnlyList<CallAgentResponse>> ListAsync(CancellationToken cancellationToken)
    {
        _ = _currentUser.RequireSupervisorId();

        return await _db.CallAgents
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new CallAgentResponse(e.Id, e.UserId, e.FullName, e.IsActive, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<CallAgentResponse> GetAsync(int callAgentId, CancellationToken cancellationToken)
    {
        _ = _currentUser.RequireSupervisorId();

        return await _db.CallAgents
                   .AsNoTracking()
                   .Where(e => e.Id == callAgentId)
                   .Select(e => new CallAgentResponse(e.Id, e.UserId, e.FullName, e.IsActive, e.CreatedAtUtc))
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("CallAgent not found.");
    }

    private static CallAgentResponse ToResponse(CallAgent e) =>
        new(e.Id, e.UserId, e.FullName, e.IsActive, e.CreatedAtUtc);
}
