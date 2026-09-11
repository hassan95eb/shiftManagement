using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Infrastructure.Persistence;

/// <summary>
/// The single scenario seed for ShiftFlow. It writes one self-consistent data
/// set in which every apply rule (CLAUDE.md §5) can be seen to <b>pass</b> and to
/// <b>fail</b> without a reviewer building any fixture by hand, plus a completed
/// approval (with its sibling rejection) and a shift with several pending
/// applicants and their recommendation rows.
/// <para>
/// Idempotent: it checks for the <see cref="SupervisorUsername"/> account and
/// returns without writing if the seed has already run, so a second startup — or
/// a second call in a test — changes nothing. Every timestamp is a fixed
/// absolute value, so the data set does not depend on the wall clock.
/// </para>
/// <para>
/// <c>database/03-seed.sql</c> is the hand-written SQL equivalent of this class
/// for the raw-script setup path (schema from <c>01-schema.sql</c> /
/// <c>02-indexes.sql</c>); the two produce the same logical rows. Neither runs
/// the other.
/// </para>
/// </summary>
/// <remarks>
/// The scenario, by row (see <c>README.md</c> -> Database Design -> Seed scenario map):
/// <list type="bullet">
///   <item><b>supervisor</b> / <b>rival</b> — two supervisors; <c>rival</c> owns a
///     separate project so the ownership boundary (CLAUDE.md §7) is visible.</item>
///   <item><b>Retail Support</b> shift on 2026-11-10 08:00–16:00 (Open) — the
///     applicant pool: <c>ada</c>, <c>nate</c> and <c>kite</c> apply, each with a
///     recommendation row; <c>lin</c> can be added by the reviewer.</item>
///   <item><b>grace</b> covers only 06:00–14:00 that day, <b>omar</b> has no
///     window that day — applying either to the Retail shift fails apply rule 3.</item>
///   <item><b>lin</b> covers the shift exactly — applying succeeds; applying a
///     second time fails apply rule 4.</item>
///   <item><b>rosa</b> is on no Retail/Billing project — applying fails apply rule 2.</item>
///   <item>The 2026-11-11 Retail shift is <c>Closed</c> (omar approved, kite
///     rejected with the "shift filled" note) — applying to it fails apply rule 1.</item>
///   <item><c>ada</c> is approved for a 2026-11-12 09:00–17:00 shift, so applying
///     to the overlapping 08:00–16:00 shift that day fails apply rule 5, while the
///     non-overlapping Billing shift on 2026-11-10 is accepted.</item>
/// </list>
/// </remarks>
public sealed class SeedData
{
    /// <summary>Username of the seeded Manager. Password: <see cref="DemoPassword"/>.</summary>
    public const string ManagerUsername = "manager";

    /// <summary>Username of the primary seeded supervisor. Password: <see cref="DemoPassword"/>.</summary>
    public const string SupervisorUsername = "supervisor";

    /// <summary>Username of the second seeded supervisor, owner of a separate project.</summary>
    public const string RivalSupervisorUsername = "rival";

    /// <summary>Shared password for every seeded account. Development data only, never a real secret.</summary>
    public const string DemoPassword = "Demo!Pass1";

    /// <summary>Fixed stamp for the bookkeeping <c>CreatedAtUtc</c> / <c>AssignedAtUtc</c> columns.</summary>
    private static readonly DateTime SeededAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<SeedData> _logger;

    public SeedData(AppDbContext db, IPasswordHasher passwordHasher, ILogger<SeedData> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Users.AnyAsync(u => u.Username == SupervisorUsername, cancellationToken))
        {
            _logger.LogInformation("Scenario seed already present; nothing inserted.");
            return;
        }

        var passwordHash = _passwordHasher.Hash(DemoPassword);

        var manager = new User
        {
            Username = ManagerUsername,
            PasswordHash = passwordHash,
            Role = UserRole.Manager,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
        };

        User SupervisorUser(string username) => new()
        {
            Username = username,
            PasswordHash = passwordHash,
            Role = UserRole.Supervisor,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
        };

        CallAgent MakeCallAgent(string username, string fullName) => new()
        {
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
            User = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                Role = UserRole.CallAgent,
                IsActive = true,
                CreatedAtUtc = SeededAtUtc,
            },
        };

        static Availability Window(DateTime startUtc, DateTime endUtc) =>
            new() { StartUtc = startUtc, EndUtc = endUtc, CreatedAtUtc = SeededAtUtc };

        static Rating Rating(string period, decimal score) =>
            new() { Period = period, Score = score, CreatedAtUtc = SeededAtUtc };

        // --- Supervisors -------------------------------------------------------
        var northwind = new Supervisor { Name = "Northwind Support", CreatedAtUtc = SeededAtUtc, User = SupervisorUser(SupervisorUsername) };
        var southwind = new Supervisor { Name = "Southwind Staffing", CreatedAtUtc = SeededAtUtc, User = SupervisorUser(RivalSupervisorUsername) };

        // --- CallAgents, availability and ratings -----------------------------
        // The month before the applicant-pool shift (2026-11) is 2026-10; those
        // ratings feed RatingScore. kite has no row on purpose — it scores 3.0.
        var ada = MakeCallAgent("ada", "Ada Lovelace");
        ada.Availabilities.Add(Window(D(11, 10, 6), D(11, 10, 22)));
        ada.Availabilities.Add(Window(D(11, 12, 6), D(11, 12, 22)));
        ada.Ratings.Add(Rating("2026-10", 4.6m));
        ada.Ratings.Add(Rating("2026-09", 4.2m));

        var grace = MakeCallAgent("grace", "Grace Hopper");
        grace.Availabilities.Add(Window(D(11, 10, 6), D(11, 10, 14))); // ends before the 16:00 shift
        grace.Ratings.Add(Rating("2026-10", 3.9m));

        var lin = MakeCallAgent("lin", "Lin Yao");
        lin.Availabilities.Add(Window(D(11, 10, 8), D(11, 10, 16))); // exact cover
        lin.Ratings.Add(Rating("2026-10", 5.0m));

        var omar = MakeCallAgent("omar", "Omar Khayyam");
        omar.Availabilities.Add(Window(D(11, 11, 7), D(11, 11, 17))); // covers the Closed shift only
        omar.Ratings.Add(Rating("2026-10", 2.4m));

        var nate = MakeCallAgent("nate", "Nate Silver");
        nate.Availabilities.Add(Window(D(11, 10, 6), D(11, 10, 20)));
        nate.Ratings.Add(Rating("2026-10", 3.1m));

        var kite = MakeCallAgent("kite", "Kite Tanaka");
        kite.Availabilities.Add(Window(D(11, 10, 6), D(11, 10, 20)));
        kite.Availabilities.Add(Window(D(11, 11, 6), D(11, 11, 18)));

        var rosa = MakeCallAgent("rosa", "Rosa Parks");
        rosa.Availabilities.Add(Window(D(11, 10, 6), D(11, 10, 22)));

        // --- Projects and assignments -------------------------------------
        var retail = new Project { Name = "Retail Support", IsActive = true, CreatedAtUtc = SeededAtUtc, Supervisor = northwind };
        var billing = new Project { Name = "Billing Support", IsActive = true, CreatedAtUtc = SeededAtUtc, Supervisor = northwind };
        var overflow = new Project { Name = "Overflow Desk", IsActive = true, CreatedAtUtc = SeededAtUtc, Supervisor = southwind };

        Assign(retail, ada, grace, lin, omar, nate, kite);
        Assign(billing, ada);
        Assign(overflow, rosa);

        // --- Shifts ------------------------------------------------------
        var retailPool = new Shift { Project = retail, StartUtc = D(11, 10, 8), EndUtc = D(11, 10, 16), Status = ShiftStatus.Open, CreatedAtUtc = SeededAtUtc };
        var billingOpen = new Shift { Project = billing, StartUtc = D(11, 10, 12), EndUtc = D(11, 10, 20), Status = ShiftStatus.Open, CreatedAtUtc = SeededAtUtc };
        var retailClosed = new Shift { Project = retail, StartUtc = D(11, 11, 8), EndUtc = D(11, 11, 16), Status = ShiftStatus.Closed, CreatedAtUtc = SeededAtUtc };
        var retailNov12 = new Shift { Project = retail, StartUtc = D(11, 12, 8), EndUtc = D(11, 12, 16), Status = ShiftStatus.Open, CreatedAtUtc = SeededAtUtc };
        var adaApprovedShift = new Shift { Project = retail, StartUtc = D(11, 12, 9), EndUtc = D(11, 12, 17), Status = ShiftStatus.Closed, CreatedAtUtc = SeededAtUtc };

        _db.Users.Add(manager);
        _db.Supervisors.AddRange(northwind, southwind);
        _db.CallAgents.AddRange(ada, grace, lin, omar, nate, kite, rosa);
        _db.Projects.AddRange(retail, billing, overflow);
        _db.Shifts.AddRange(retailPool, billingOpen, retailClosed, retailNov12, adaApprovedShift);

        // --- Applications ---------------------------------------------------
        // The pool on the open Retail shift.
        _db.ShiftApplications.Add(Pending(retailPool, ada, D(11, 1, 9)));
        _db.ShiftApplications.Add(Pending(retailPool, nate, D(11, 1, 10)));
        _db.ShiftApplications.Add(Pending(retailPool, kite, D(11, 2, 8)));

        // The completed decision on the Closed Retail shift: omar approved, kite
        // rejected with the exact note the approval cascade writes.
        _db.ShiftApplications.Add(Decided(retailClosed, omar, ApplicationStatus.Approved, D(10, 28, 9), northwind.User, D(10, 30, 12), decisionNote: null));
        _db.ShiftApplications.Add(Decided(retailClosed, kite, ApplicationStatus.Rejected, D(10, 28, 10), northwind.User, D(10, 30, 12), "Shift filled by another CallAgent."));

        // ada already holds an approved 09:00–17:00 shift on 2026-11-12.
        _db.ShiftApplications.Add(Decided(adaApprovedShift, ada, ApplicationStatus.Approved, D(10, 29, 9), northwind.User, D(11, 1, 8), decisionNote: null));

        // --- Recommendations ---------------------------------------------
        // Read-only rows the Python recommender would produce; seeded so the
        // supervisor ranking endpoint returns a meaningful list before that script
        // exists. Numbers follow the CLAUDE.md §5 formula for the 8h Retail shift.
        var computedAtUtc = D(11, 3, 6);
        _db.Recommendations.Add(new Recommendation
        {
            Shift = retailPool,
            CallAgent = ada,
            Score = 76.10m,
            Reason = "Rating 4.6/5 -> 27.6 | Workload 8h -> 28.5 | Availability 8/16h -> 20.0 | Total 76.1",
            ComputedAtUtc = computedAtUtc,
        });
        _db.Recommendations.Add(new Recommendation
        {
            Shift = retailPool,
            CallAgent = nate,
            Score = 71.50m,
            Reason = "Rating 3.1/5 -> 18.6 | Workload 0h -> 30.0 | Availability 8/14h -> 22.9 | Total 71.5",
            ComputedAtUtc = computedAtUtc,
        });
        _db.Recommendations.Add(new Recommendation
        {
            Shift = retailPool,
            CallAgent = kite,
            Score = 70.90m,
            Reason = "Rating default 3.0/5 -> 18.0 | Workload 0h -> 30.0 | Availability 8/14h -> 22.9 | Total 70.9",
            ComputedAtUtc = computedAtUtc,
        });

        // One SaveChanges: EF wraps the whole graph in a single transaction, so a
        // failure leaves nothing behind and the next startup retries cleanly.
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Scenario seed inserted: manager account '{Manager}', supervisor accounts '{Supervisor}' and "
            + "'{Rival}', seven CallAgents, three projects and five shifts. See README.md for the seed scenario map.",
            ManagerUsername,
            SupervisorUsername,
            RivalSupervisorUsername);
    }

    /// <summary>2026 UTC date at a whole hour, so every value round-trips through <c>datetime2(0)</c>.</summary>
    private static DateTime D(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static void Assign(Project project, params CallAgent[] callAgents)
    {
        foreach (var callAgent in callAgents)
        {
            project.CallAgentProjects.Add(new CallAgentProject { CallAgent = callAgent, AssignedAtUtc = SeededAtUtc });
        }
    }

    private static ShiftApplication Pending(Shift shift, CallAgent callAgent, DateTime appliedAtUtc) => new()
    {
        Shift = shift,
        CallAgent = callAgent,
        Status = ApplicationStatus.Pending,
        AppliedAtUtc = appliedAtUtc,
    };

    private static ShiftApplication Decided(
        Shift shift,
        CallAgent callAgent,
        ApplicationStatus status,
        DateTime appliedAtUtc,
        User decidedBy,
        DateTime decidedAtUtc,
        string? decisionNote) => new()
    {
        Shift = shift,
        CallAgent = callAgent,
        Status = status,
        AppliedAtUtc = appliedAtUtc,
        DecidedByUser = decidedBy,
        DecidedAtUtc = decidedAtUtc,
        DecisionNote = decisionNote,
    };
}
