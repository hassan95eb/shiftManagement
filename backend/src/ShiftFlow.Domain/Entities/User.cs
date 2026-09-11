using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Domain.Entities;

/// <summary>
/// Authentication identity. Kept separate from <see cref="Supervisor"/> /
/// <see cref="CallAgent"/> so a future role does not disturb it
/// (docs/01-erd-and-schema.md §3-1).
/// </summary>
public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation — one User is exactly one Supervisor or one CallAgent (1:1, optional
    // on this side).
    public Supervisor? Supervisor { get; set; }

    public CallAgent? CallAgent { get; set; }

    // Applications this user decided on, as the deciding Supervisor's account
    // (ShiftApplications.DecidedByUserId, NO ACTION on delete).
    public ICollection<ShiftApplication> DecidedApplications { get; } = new List<ShiftApplication>();
}
