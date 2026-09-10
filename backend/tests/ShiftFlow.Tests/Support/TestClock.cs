using System;
using ShiftFlow.Application.Abstractions;

namespace ShiftFlow.Tests.Support;

/// <summary>A settable <see cref="IClock"/> so timestamps in these tests are deterministic.</summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTime? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; set; }
}
