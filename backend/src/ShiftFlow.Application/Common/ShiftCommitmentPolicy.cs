using System.Linq.Expressions;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Common;

/// <summary>One definition of a shift committed to a particular CallAgent.</summary>
public static class ShiftCommitmentPolicy
{
    public static Expression<Func<Shift, bool>> ForCallAgent(int callAgentId) =>
        shift =>
            (shift.Status == ShiftStatus.Assigned && shift.AssignedCallAgentId == callAgentId)
            || (shift.Status == ShiftStatus.Closed && shift.ShiftApplications.Any(application =>
                application.CallAgentId == callAgentId
                && application.Status == ApplicationStatus.Approved));

    public static bool IsCommittedTo(Shift shift, int callAgentId) =>
        (shift.Status == ShiftStatus.Assigned && shift.AssignedCallAgentId == callAgentId)
        || (shift.Status == ShiftStatus.Closed && shift.ShiftApplications.Any(application =>
            application.CallAgentId == callAgentId
            && application.Status == ApplicationStatus.Approved));
}
