using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using Xunit;

namespace Backend.Tests;

// The SM-01 / SM-02 / SM-03 guard tables are the branchy security-path logic behind
// TaskAssignmentService, RequestReviewService and UserService. If a transition opens up by
// accident, these fail.
// Reference: Satzinger, J.W., Jackson, R.B. and Burd, S.D. (2016) Systems analysis and design in a changing world. 7th edn. Boston: Cengage Learning.
public class LifecycleTests
{
    [Theory]
    [InlineData(null, "In Progress", true)]   // unset treated as "To Do"
    [InlineData("To Do", "In Progress", true)]
    [InlineData("To Do", "Done", false)]      // cannot skip straight to Done
    [InlineData("To Do", "Blocked", false)]   // only a started task can be blocked
    [InlineData("To Do", "Cancelled", true)]
    [InlineData("In Progress", "Cancelled", true)]
    [InlineData("Blocked", "Cancelled", true)]   // event cancellation (test plan M3)
    [InlineData("In Progress", "Done", true)]
    [InlineData("Done", "In Progress", true)] // reopen allowed
    [InlineData("Done", "Archived", true)]
    [InlineData("Done", "To Do", false)]
    [InlineData("Cancelled", "In Progress", false)]
    [InlineData("Archived", "In Progress", false)]
    [InlineData("In Progress", "In Progress", true)] // idempotent no-op
    public void TaskTransitions(string? from, string to, bool allowed) =>
        Assert.Equal(allowed, Lifecycle.TaskCanMove(from, to));
//----------------------------------------------------------------------------------------------------------------------//
    [Theory]
    [InlineData(null, "Under Review", true)]
    [InlineData("Pending", "Approved", false)]       // must be under review first
    [InlineData("Pending", "Escalated", true)]
    [InlineData("Escalated", "Under Review", true)]
    [InlineData("Escalated", "Approved", false)]
    [InlineData("Under Review", "Approved", true)]
    [InlineData("Under Review", "Declined", true)]
    [InlineData("Declined", "Pending", true)]        // resubmit
    [InlineData("Declined", "Approved", false)]
    [InlineData("Approved", "Completed", true)]
    [InlineData("Completed", "Approved", false)]
    public void RequestTransitions(string? from, string to, bool allowed) =>
        Assert.Equal(allowed, Lifecycle.RequestCanMove(from, to));
//----------------------------------------------------------------------------------------------------------------------//
    [Theory]
    [InlineData("Pending Activation", "Active", true)]
    [InlineData("Pending Activation", "Locked", false)]
    [InlineData("Active", "Locked", true)]
    [InlineData("Active", "Inactive", true)]
    [InlineData("Active", "Anonymised", false)]      // must be deactivated first
    [InlineData("Locked", "Active", true)]
    [InlineData("Locked", "Inactive", false)]
    [InlineData("Inactive", "Active", true)]
    [InlineData("Inactive", "Anonymised", true)]
    [InlineData("Anonymised", "Active", false)]      // terminal
    public void UserTransitions(string from, string to, bool allowed) =>
        Assert.Equal(allowed, Lifecycle.UserCanMove(from, to));
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public void MoveUser_keeps_IsActive_in_step()
    {
        var user = new User { Status = Lifecycle.UserActive, IsActive = true };
        Lifecycle.MoveUser(user, Lifecycle.UserLocked);
        Assert.False(user.IsActive);
        Lifecycle.MoveUser(user, Lifecycle.UserActive);
        Assert.True(user.IsActive);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public void EnsureRequestMove_throws_on_illegal_transition() =>
        Assert.Throws<InvalidOperationException>(() => Lifecycle.EnsureRequestMove("Declined", "Approved"));
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//