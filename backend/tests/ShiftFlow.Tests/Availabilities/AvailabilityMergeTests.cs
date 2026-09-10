using System;
using System.Collections.Generic;
using System.Linq;
using ShiftFlow.Application.Features.Availabilities;

namespace ShiftFlow.Tests.Availabilities;

/// <summary>
/// The merge algorithm on its own (CLAUDE.md §5, docs/01 §3-6). "Adjacent" at
/// the boundary means one window ending at the exact instant another begins:
/// that pair must merge, because a shift spanning that instant is continuous
/// work. The result must depend only on the union of the inputs, never on the
/// order they arrived in.
/// </summary>
public class AvailabilityMergeTests
{
    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static TimeRange Window(int startHour, int endHour) => new(At(startHour), At(endHour));

    private static (DateTime StartUtc, DateTime EndUtc)[] Shape(IEnumerable<TimeRange> windows) =>
        windows.Select(w => (w.StartUtc, w.EndUtc)).ToArray();

    [Fact]
    public void Adjacent_windows_merge_touching_endpoints_close_the_gap()
    {
        var merged = AvailabilityMerge.Merge([Window(8, 12), Window(12, 16)]);

        Assert.Equal([(At(8), At(16))], Shape(merged));
    }

    [Fact]
    public void Adjacent_windows_merge_to_the_same_result_in_either_insertion_order()
    {
        var forward = AvailabilityMerge.Merge([Window(8, 12), Window(12, 16)]);
        var reverse = AvailabilityMerge.Merge([Window(12, 16), Window(8, 12)]);

        Assert.Equal(Shape(forward), Shape(reverse));
        Assert.Equal([(At(8), At(16))], Shape(forward));
    }

    [Fact]
    public void Overlapping_windows_merge_into_their_span()
    {
        var merged = AvailabilityMerge.Merge([Window(8, 13), Window(11, 16)]);

        Assert.Equal([(At(8), At(16))], Shape(merged));
    }

    [Fact]
    public void A_window_that_swallows_several_collapses_them_into_one()
    {
        var merged = AvailabilityMerge.Merge(
        [
            Window(9, 10),
            Window(11, 12),
            Window(13, 14),
            Window(7, 18),
        ]);

        Assert.Equal([(At(7), At(18))], Shape(merged));
    }

    [Fact]
    public void Non_adjacent_windows_are_left_separate_and_sorted()
    {
        var merged = AvailabilityMerge.Merge([Window(14, 16), Window(8, 10)]);

        Assert.Equal([(At(8), At(10)), (At(14), At(16))], Shape(merged));
    }

    [Fact]
    public void A_one_hour_gap_between_windows_is_not_bridged()
    {
        var merged = AvailabilityMerge.Merge([Window(8, 10), Window(11, 13)]);

        Assert.Equal([(At(8), At(10)), (At(11), At(13))], Shape(merged));
    }
}
