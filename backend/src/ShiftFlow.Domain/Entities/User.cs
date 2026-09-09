using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Domain.Entities;

/// <summary>
/// Authentication identity. Kept separate from <see cref="Employer"/> /
/// <see cref="Expert"/> so a future role does not disturb it
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

    // Navigation — one User is exactly one Employer or one Expert (1:1, optional
    // on this side).
    public Employer? Employer { get; set; }

    public Expert? Expert { get; set; }

    // Applications this user decided on, as the deciding Employer's account
    // (ShiftApplications.DecidedByUserId, NO ACTION on delete).
    public ICollection<ShiftApplication> DecidedApplications { get; } = new List<ShiftApplication>();
}
