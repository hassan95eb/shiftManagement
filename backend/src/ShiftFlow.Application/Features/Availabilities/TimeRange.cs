namespace ShiftFlow.Application.Features.Availabilities;

/// <summary>
/// A half-open-agnostic UTC interval used by the availability merge and the
/// approved-shift coverage check. Both bounds are whole instants (the
/// <c>Availabilities</c> columns are <c>datetime2(0)</c>), and
/// <see cref="EndUtc"/> is always strictly after <see cref="StartUtc"/> — the
/// service validates that before constructing one.
/// </summary>
public readonly record struct TimeRange(DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>
    /// <c>true</c> when <paramref name="inner"/> lies entirely within this
    /// range, endpoints included. This is the containment test behind
    /// "the whole shift must fall inside one availability window"
    /// (CLAUDE.md §5).
    /// </summary>
    public bool Covers(TimeRange inner) =>
        StartUtc <= inner.StartUtc && inner.EndUtc <= EndUtc;
}
