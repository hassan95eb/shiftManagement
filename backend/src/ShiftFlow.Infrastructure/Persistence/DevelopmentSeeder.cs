using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Infrastructure.Persistence;

/// <summary>
/// Inserts the smallest self-consistent set of rows that makes the API usable
/// by hand through Swagger before the phase-10 scenario seed exists: one
/// employer account and one expert account, a project owned by that employer
/// with the expert assigned to it, one <see cref="ShiftStatus.Open"/> shift in
/// the near future, and an availability window that fully covers that shift.
/// <para>
/// Idempotent: it looks for the employer account first and returns without
/// writing if the seed has already run, so a second startup changes nothing.
/// This is only a login path for manual testing — it is deliberately not the
/// phase-10 seed (CLAUDE.md §10), which is still written from scratch there and
/// kept independent of this code.
/// </para>
/// </summary>
public sealed class DevelopmentSeeder
{
    /// <summary>Username of the seeded employer account. Password: <see cref="DemoPassword"/>.</summary>
    public const string EmployerUsername = "demo-employer";

    /// <summary>Username of the seeded expert account. Password: <see cref="DemoPassword"/>.</summary>
    public const string ExpertUsername = "demo-expert";

    /// <summary>Shared password for both seeded accounts. Development-only, never a real secret.</summary>
    public const string DemoPassword = "Demo!Pass1";

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly ILogger<DevelopmentSeeder> _logger;

    public DevelopmentSeeder(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILogger<DevelopmentSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development seed accounts — employer: {EmployerUsername} / expert: {ExpertUsername} (password: {Password}).",
            EmployerUsername,
            ExpertUsername,
            DemoPassword);

        if (await _db.Users.AnyAsync(u => u.Username == EmployerUsername, cancellationToken))
        {
            _logger.LogInformation("Development seed already present; nothing inserted.");
            return;
        }

        var now = _clock.UtcNow;

        var employer = new Employer
        {
            Name = "Demo Call Center",
            CreatedAtUtc = now,
            User = new User
            {
                Username = EmployerUsername,
                PasswordHash = _passwordHasher.Hash(DemoPassword),
                Role = UserRole.Employer,
                IsActive = true,
                CreatedAtUtc = now,
            },
        };

        var expert = new Expert
        {
            FullName = "Demo Expert",
            IsActive = true,
            CreatedAtUtc = now,
            User = new User
            {
                Username = ExpertUsername,
                PasswordHash = _passwordHasher.Hash(DemoPassword),
                Role = UserRole.Expert,
                IsActive = true,
                CreatedAtUtc = now,
            },
        };

        var project = new Project
        {
            Name = "Demo Project",
            IsActive = true,
            CreatedAtUtc = now,
            Employer = employer,
        };

        project.ExpertProjects.Add(new ExpertProject
        {
            Expert = expert,
            AssignedAtUtc = now,
        });

        // Tomorrow 09:00–17:00 UTC. Whole seconds, so it round-trips through the
        // datetime2(0) columns unchanged.
        var shiftStart = now.Date.AddDays(1).AddHours(9);
        var shift = new Shift
        {
            Project = project,
            StartUtc = shiftStart,
            EndUtc = shiftStart.AddHours(8),
            Status = ShiftStatus.Open,
            CreatedAtUtc = now,
        };

        // One hour of slack on each side so the shift falls strictly inside the
        // window — apply-rule 3 (CLAUDE.md §5) is then satisfiable straight away.
        var availability = new Availability
        {
            Expert = expert,
            StartUtc = shiftStart.AddHours(-1),
            EndUtc = shiftStart.AddHours(9),
            CreatedAtUtc = now,
        };

        _db.Employers.Add(employer);
        _db.Experts.Add(expert);
        _db.Projects.Add(project);
        _db.Shifts.Add(shift);
        _db.Availabilities.Add(availability);

        // One SaveChanges — EF wraps the whole graph in a single transaction, so
        // a failure leaves nothing behind and the next startup retries cleanly.
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Development seed inserted: project '{Project}' with one open shift {Start:u} – {End:u}.",
            project.Name,
            shift.StartUtc,
            shift.EndUtc);
    }
}
