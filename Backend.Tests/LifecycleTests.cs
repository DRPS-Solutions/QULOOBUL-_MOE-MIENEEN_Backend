using QULOOBUL__MOE_MIENEEN_Backend.Data;
using Xunit;

namespace Backend.Tests;

// The SM-01 / SM-02 guard tables are the branchy security-path logic behind
// TaskAssignmentService and RequestReviewService. If a transition opens up by accident,
// these fail.
public class LifecycleTests
{
    [Theory]
    [InlineData(null, "In Progress", true)]   // unset treated as "To Do"
    [InlineData("To Do", "In Progress", true)]
    [InlineData("To Do", "Done", false)]      // cannot skip straight to Done
    [InlineData("In Progress", "Done", true)]
    [InlineData("Done", "In Progress", true)] // reopen allowed
    [InlineData("Done", "To Do", false)]
    [InlineData("In Progress", "In Progress", true)] // idempotent no-op
    public void TaskTransitions(string? from, string to, bool allowed) =>
        Assert.Equal(allowed, Lifecycle.TaskCanMove(from, to));

    [Theory]
    [InlineData(null, "Approved", true)]
    [InlineData("Pending", "Declined", true)]
    [InlineData("Pending", "Completed", false)]  // must be approved first
    [InlineData("Approved", "Completed", true)]
    [InlineData("Declined", "Approved", false)]  // declined is terminal
    [InlineData("Completed", "Approved", false)]
    public void RequestTransitions(string? from, string to, bool allowed) =>
        Assert.Equal(allowed, Lifecycle.RequestCanMove(from, to));

    [Fact]
    public void EnsureRequestMove_throws_on_illegal_transition() =>
        Assert.Throws<InvalidOperationException>(() => Lifecycle.EnsureRequestMove("Declined", "Approved"));
}
