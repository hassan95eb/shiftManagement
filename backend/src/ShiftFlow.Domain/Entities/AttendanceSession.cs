namespace ShiftFlow.Domain.Entities;

/// <summary>
/// A bounded record of a CallAgent's presence during one shift. Open sessions
/// retain a null <see cref="EndedAtUtc"/>; staleness is derived from
/// <see cref="LastSeenUtc"/> and never persisted as another state.
/// </summary>
public class AttendanceSession
{
    public int Id { get; set; }

    public int CallAgentId { get; set; }

    public int ShiftId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public CallAgent CallAgent { get; set; } = null!;

    public Shift Shift { get; set; } = null!;
}
