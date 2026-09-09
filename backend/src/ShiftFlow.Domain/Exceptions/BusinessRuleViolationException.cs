namespace ShiftFlow.Domain.Exceptions;

/// <summary>
/// Raised when a business rule is broken — an apply-to-shift rule, an approval
/// pre-condition, or an availability constraint (CLAUDE.md §5).
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message)
        : base(message)
    {
    }

    public BusinessRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
