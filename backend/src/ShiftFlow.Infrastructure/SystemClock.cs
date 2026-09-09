using ShiftFlow.Application.Abstractions;

namespace ShiftFlow.Infrastructure;

/// <summary>
/// Production <see cref="IClock"/> — reads the machine clock in UTC. Tests
/// substitute a fixed clock instead (CLAUDE.md §4).
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
