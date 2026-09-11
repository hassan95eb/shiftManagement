namespace ShiftFlow.Application.Features.Availabilities;

/// <summary>
/// Normalizes a set of availability windows for one CallAgent so the stored set
/// never holds two windows that overlap or touch (docs/01-erd-and-schema.md
/// §3-6, CLAUDE.md §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>"Adjacent" merges.</b> Two windows where one ends at the exact instant the
/// next begins (<c>a.EndUtc == b.StartUtc</c>) are joined into one. A shift that
/// spans that instant is continuous work, so the availability behind it must be
/// a single unbroken window — hence the boundary comparison is <c>&lt;=</c>, not
/// <c>&lt;</c>. This is deliberately the opposite of the shift-overlap rule
/// (CLAUDE.md §5 rule 5), which is half-open so that back-to-back shifts such as
/// 10–14 and 14–18 do <i>not</i> conflict.
/// </para>
/// <para>
/// <b>Order independence.</b> The result is a pure function of the union of the
/// inputs: the inputs are sorted by start before the sweep, so inserting
/// 08–12 then 12–16 and inserting 12–16 then 08–12 both normalize to the same
/// single 08–16 window.
/// </para>
/// </remarks>
public static class AvailabilityMerge
{
    /// <summary>
    /// Returns the windows sorted by start, with every overlapping or touching
    /// pair collapsed into one.
    /// </summary>
    public static IReadOnlyList<TimeRange> Merge(IEnumerable<TimeRange> windows)
    {
        var ordered = windows
            .OrderBy(w => w.StartUtc)
            .ThenBy(w => w.EndUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<TimeRange>();
        }

        var merged = new List<TimeRange>();
        var current = ordered[0];

        for (var i = 1; i < ordered.Count; i++)
        {
            var next = ordered[i];

            // Sorted by start, so current.StartUtc <= next.StartUtc already.
            // next.StartUtc == current.EndUtc (touching) merges too — hence <=.
            if (next.StartUtc <= current.EndUtc)
            {
                if (next.EndUtc > current.EndUtc)
                {
                    current = current with { EndUtc = next.EndUtc };
                }
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }
}
