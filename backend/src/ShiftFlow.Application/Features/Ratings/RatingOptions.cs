namespace ShiftFlow.Application.Features.Ratings;

/// <summary>
/// Configuration used by rating inputs. V8 adds the rating weights to this
/// same class so all Rating-section settings keep one strongly typed contract.
/// </summary>
public sealed class RatingOptions
{
    public const string SectionName = "Rating";

    public decimal DowntimeCapHours { get; init; } = 8m;
}
