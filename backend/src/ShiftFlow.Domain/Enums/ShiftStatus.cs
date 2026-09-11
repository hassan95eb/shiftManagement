namespace ShiftFlow.Domain.Enums;

/// <summary>
/// Lifecycle of a shift. Stored as NVARCHAR(16) with a CHECK constraint,
/// default <see cref="Open"/> (docs/01-erd-and-schema.md §3-7, §7).
/// </summary>
/// <remarks>
/// Transitions belong to exactly one operation each and <see cref="Closed"/> is
/// reachable only from <see cref="Open"/>, only through an <c>Extra</c>
/// application approval:
/// <list type="bullet">
///   <item><see cref="Open"/> → <see cref="Assigned"/> — the assignment endpoint.</item>
///   <item><see cref="Assigned"/> → <see cref="Open"/> — the assignment endpoint's
///   delete, only while no attendance exists.</item>
///   <item><see cref="Open"/> → <see cref="Closed"/> — the Extra application
///   approval transaction.</item>
///   <item><see cref="Assigned"/> → <see cref="Released"/> — leave approval (V5).</item>
///   <item><see cref="Released"/> → <see cref="Assigned"/> — the assignment
///   endpoint (direct fill) or a Cover application approval (V6).</item>
/// </list>
/// </remarks>
public enum ShiftStatus
{
    Open,
    Assigned,
    Released,
    Closed,
}
