namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// The single source of "now" for the whole application. Services depend on
/// this instead of calling <see cref="System.DateTime.UtcNow"/> directly so
/// month-based logic (approved-hours windows, rating periods) stays
/// deterministic under test (CLAUDE.md §4).
/// </summary>
public interface IClock
{
    /// <summary>Current instant in UTC. Never local time.</summary>
    DateTime UtcNow { get; }
}
