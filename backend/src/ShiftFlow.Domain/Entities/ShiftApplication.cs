using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Domain.Entities;

/// <summary>
/// An expert's application to a shift. Unique on (ShiftId, ExpertId); a filtered
/// unique index allows at most one <see cref="ApplicationStatus.Approved"/> row
/// per shift. The three Decided* columns are the decision audit trail
/// (docs/01-erd-and-schema.md §3-8). Shift side CASCADE, Expert and
/// DecidedByUser sides NO ACTION (docs/01 §4).
/// </summary>
public class ShiftApplication
{
    public int Id { get; set; }

    // FK -> Shifts. CASCADE on delete.
    public int ShiftId { get; set; }

    // FK -> Experts. NO ACTION on delete.
    public int ExpertId { get; set; }

    public ApplicationStatus Status { get; set; }

    // Also the tie-breaker when two applicants score equally (CLAUDE.md §5).
    public DateTime AppliedAtUtc { get; set; }

    // Null until decided. FK -> Users, NO ACTION on delete.
    public int? DecidedByUserId { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public string? DecisionNote { get; set; }

    // Navigation
    public Shift Shift { get; set; } = null!;

    public Expert Expert { get; set; } = null!;

    public User? DecidedByUser { get; set; }
}
