using FluentAssertions;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Models;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// Does an empty shortlist explain itself?
/// </summary>
/// <remarks>
/// <para>
/// The shortlist response carried no reason, and the client's "No matches found"
/// panel read a key that was never emitted. So an empty list showed an empty list:
/// the reader could not tell whether the AI had found nobody suitable or had simply
/// not finished looking.
/// </para>
/// <para>
/// Both halves are load-bearing. The endpoint had to start sending the key, and the
/// client had to read it under the name the endpoint sends. Either alone leaves the
/// panel dead, which is what shipped.
/// </para>
/// </remarks>
public sealed class EmptyShortlistExplanationTests
{
    [Theory]
    [InlineData(WorkflowRunStatus.Pending, null)]
    [InlineData(WorkflowRunStatus.Running, null)]
    [InlineData(WorkflowRunStatus.Completed, "nobody scored high enough")]
    [InlineData(WorkflowRunStatus.Failed, "failed before it produced a shortlist")]
    [InlineData(WorkflowRunStatus.Cancelled, "cancelled")]
    [InlineData(WorkflowRunStatus.PendingApproval, "did not pass its checks")]
    [InlineData(WorkflowRunStatus.PendingManagerApproval, "waiting on a manager review")]
    [InlineData(WorkflowRunStatus.PendingClientApproval, "nobody scored high enough")]
    public void Says_something_useful_when_the_shortlist_is_empty(
        WorkflowRunStatus status, string? expectedFragment)
    {
        var reason = AiTalentMatchController.DescribeEmptyShortlist(
            new WorkflowRun { Status = status }, recommendationCount: 0);

        if (expectedFragment is null)
        {
            // While the run is live, "no matches" would be a claim nobody has made
            // yet -- and it would flicker away on the next poll, which reads as a
            // glitch rather than as progress.
            reason.Should().BeNull();
            return;
        }

        reason.Should().NotBeNull();
        reason!.ToLowerInvariant().Should().Contain(expectedFragment);
    }

    [Fact]
    public void Says_nothing_when_there_are_recommendations()
    {
        // The key is still emitted, as null. Omitting it entirely would make the
        // client's read look like a missing field rather than a settled answer.
        AiTalentMatchController
            .DescribeEmptyShortlist(new WorkflowRun { Status = WorkflowRunStatus.Completed }, 4)
            .Should().BeNull();
    }

    [Fact]
    public void An_unrecognised_status_still_explains_itself()
    {
        // Falling through to null on a status this build has not seen would restore
        // the original bug for the next status anyone adds.
        var reason = AiTalentMatchController.DescribeEmptyShortlist(
            new WorkflowRun { Status = (WorkflowRunStatus)999 }, recommendationCount: 0);

        reason.Should().NotBeNull();
        reason!.ToLowerInvariant().Should().Contain("nobody scored high enough");
    }

    [Fact]
    public void The_pending_client_approval_case_is_reachable_with_no_rows()
    {
        // The one status where an empty shortlist is a genuine contradiction worth
        // naming: the run is waiting for the client to approve a list that is not
        // there. This is the state a failed matching run lands in.
        var reason = AiTalentMatchController.DescribeEmptyShortlist(
            new WorkflowRun { Status = WorkflowRunStatus.PendingClientApproval }, 0);

        reason.Should().NotBeNull();
    }
}