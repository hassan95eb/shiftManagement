using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts.Dtos;
using ShiftFlow.Application.Features.Experts.Validators;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Experts;

/// <summary>
/// Expert use cases available to an employer. Experts are a shared specialist
/// pool — the schema has no employer-ownership column on <c>Experts</c> — so the
/// list and lookup are not employer-scoped; the ownership boundary that matters
/// is on the assignment side (<see cref="ExpertProjectService"/>), which is
/// rooted on the project. All endpoints still require an Employer principal.
/// </summary>
public sealed class ExpertService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;

    public ExpertService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    /// <summary>
    /// Creates the <c>Users</c> row (role Expert, hashed password) and the
    /// <c>Experts</c> row together. Both inserts go in one
    /// <see cref="IAppDbContext.SaveChangesAsync"/> call, so EF wraps them in a
    /// single transaction — a failure on either leaves neither behind.
    /// </summary>
    public async Task<ExpertResponse> CreateAsync(CreateExpertRequest request, CancellationToken cancellationToken)
    {
        // Guarantees the caller is a provisioned employer before any write.
        _ = _currentUser.RequireEmployerId();

        var (username, password, fullName) = CreateExpertRequestValidator.ValidateAndNormalize(request);

        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == username, cancellationToken);
        if (usernameTaken)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["username"] = ["This username is already taken."],
            });
        }

        var now = _clock.UtcNow;
        var expert = new Expert
        {
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = now,
            User = new User
            {
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Role = UserRole.Expert,
                IsActive = true,
                CreatedAtUtc = now,
            },
        };

        _db.Experts.Add(expert);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(expert);
    }

    public async Task<IReadOnlyList<ExpertResponse>> ListAsync(CancellationToken cancellationToken)
    {
        _ = _currentUser.RequireEmployerId();

        return await _db.Experts
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new ExpertResponse(e.Id, e.UserId, e.FullName, e.IsActive, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpertResponse> GetAsync(int expertId, CancellationToken cancellationToken)
    {
        _ = _currentUser.RequireEmployerId();

        return await _db.Experts
                   .AsNoTracking()
                   .Where(e => e.Id == expertId)
                   .Select(e => new ExpertResponse(e.Id, e.UserId, e.FullName, e.IsActive, e.CreatedAtUtc))
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("Expert not found.");
    }

    private static ExpertResponse ToResponse(Expert e) =>
        new(e.Id, e.UserId, e.FullName, e.IsActive, e.CreatedAtUtc);
}
