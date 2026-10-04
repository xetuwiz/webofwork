using FluentAssertions;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// The stage vocabulary and its two approval gates.
/// </summary>
/// <remarks>
/// These are the rules the whole agent pipeline branches on, and they are all pure
/// functions on purpose. A mistake here does not throw at the call site -- it sends
/// a project to the wrong agent, or parks a run on a gate nobody is watching, and
/// the symptom shows up much later in a place that does not point back here.
/// </remarks>
public class WorkflowStageTests
{
    // ── the vocabulary ────────────────────────────────────────────────────

    [Theory]
    [InlineData("draft")]
    [InlineData("talent_match")]
    [InlineData("screen")]
    [InlineData("validate")]
    [InlineData("outreach")]
    public void KnownStages_AreAccepted(string stage)
    {
        WorkflowStages.IsKnown(stage).Should().BeTrue();
        WorkflowStages.Normalise(stage).Should().Be(stage);
    }

    [Theory]
    [InlineData("Draft", "draft")]           // PascalCase
    [InlineData("  talent_match  ", "talent_match")]  // padded
    [InlineData("SCREEN", "screen")]         // upper
    public void Normalise_TrimsAndLowercases(string input, string expected)
    {
        WorkflowStages.Normalise(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("matching")]     // the CLR enum name, which is NOT the wire name
    [InlineData("talentmatch")]
    [InlineData("brief_analysis")]
    public void Normalise_RejectsAnythingOutsideTheVocabulary(string? input)
    {
        WorkflowStages.IsKnown(input).Should().BeFalse();

        // Rejected rather than defaulted. A silent default would run the wrong
        // agent graph and report success, which is far worse than a 400.
        var act = () => WorkflowStages.Normalise(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Normalise_ErrorMessage_ListsTheValidStages()
    {
        // A client that sends a bad stage needs to be told what the valid ones
        // are, not just that its value is wrong.
        var ex = Assert.Throws<ArgumentException>(() => WorkflowStages.Normalise("nope"));
        ex.Message.Should().Contain("draft");
        ex.Message.Should().Contain("talent_match");
    }

    [Fact]
    public void All_ContainsEveryStage_AndNothingElse()
    {
        // `All` feeds the check constraint in the migration, so a stage missing
        // from here would be one the database rejects and the application accepts.
        WorkflowStages.All.Should().BeEquivalentTo(new[]
        {
            WorkflowStages.Draft,
            WorkflowStages.TalentMatch,
            WorkflowStages.Screen,
            WorkflowStages.Validate,
            WorkflowStages.Outreach,
        });
    }

    // ── the two gates ─────────────────────────────────────────────────────

    [Fact]
    public void TalentMatch_PausesOnTheClient()
    {
        // The client owns the project and chooses who to approach.
        WorkflowStages.ApprovalGateFor(WorkflowStages.TalentMatch)
            .Should().Be(WorkflowRunStatus.PendingClientApproval);
    }

    [Fact]
    public void Validate_PausesOnAManager()
    {
        // An admin signs off before money moves.
        WorkflowStages.ApprovalGateFor(WorkflowStages.Validate)
            .Should().Be(WorkflowRunStatus.PendingManagerApproval);
    }

    [Theory]
    [InlineData(WorkflowStages.Draft)]
    [InlineData(WorkflowStages.Screen)]
    [InlineData(WorkflowStages.Outreach)]
    public void OtherStages_HaveNoGate(string stage)
    {
        WorkflowStages.ApprovalGateFor(stage).Should().BeNull();
    }

    [Fact]
    public void TheTwoGates_AreDistinctValues()
    {
        // The whole reason the split exists. While both were `PendingApproval`, a
        // client polling "is this waiting on me?" could not be answered.
        WorkflowRunStatus.PendingClientApproval
            .Should().NotBe(WorkflowRunStatus.PendingManagerApproval);
    }

    [Fact]
    public void TheTwoGates_AreDistinctFromTheLegacyGenericValue()
    {
        // `PendingApproval` keeps its original value of 2 so no stored row changes
        // meaning. The new states must not collide with it.
        WorkflowRunStatus.PendingApproval
            .Should().NotBe(WorkflowRunStatus.PendingClientApproval);
        WorkflowRunStatus.PendingApproval
            .Should().NotBe(WorkflowRunStatus.PendingManagerApproval);
    }

    [Fact]
    public void StoredStatusValues_AreUnchanged()
    {
        // These are persisted as integers. Renumbering any of them would silently
        // reinterpret existing rows, so they are pinned here as a tripwire.
        ((int)WorkflowRunStatus.Pending).Should().Be(0);
        ((int)WorkflowRunStatus.Running).Should().Be(1);
        ((int)WorkflowRunStatus.PendingApproval).Should().Be(2);
        ((int)WorkflowRunStatus.ValidationFailed).Should().Be(3);
        ((int)WorkflowRunStatus.Approved).Should().Be(4);
        ((int)WorkflowRunStatus.Rejected).Should().Be(5);
        ((int)WorkflowRunStatus.Completed).Should().Be(6);
        ((int)WorkflowRunStatus.Failed).Should().Be(7);
        ((int)WorkflowRunStatus.Cancelled).Should().Be(8);
        ((int)WorkflowRunStatus.PendingClientApproval).Should().Be(9);
        ((int)WorkflowRunStatus.PendingManagerApproval).Should().Be(10);
    }
}

/// <summary>
/// Where a project joins the pipeline.
/// </summary>
/// <remarks>
/// Tested through the pure <c>Resolve</c> overload. The database-backed method is
/// a thin wrapper around three counts, and the part that can be wrong is the
/// precedence between them -- which is exactly what a fixture obscures.
/// </remarks>
public class StageResolverTests
{
    [Fact]
    public void NoProposalsNoShortlist_StartsAtTalentMatch()
    {
        var result = StageResolver.Resolve(hasProposals: false, hasShortlist: false, hasValidation: false);

        result.Stage.Should().Be(WorkflowStages.TalentMatch);
        result.Rationale.Should().Contain("No proposals");
    }

    [Fact]
    public void ProposalsWithoutValidation_ResumesAtScreening()
    {
        var result = StageResolver.Resolve(hasProposals: true, hasShortlist: false, hasValidation: false, proposalCount: 4);

        result.Stage.Should().Be(WorkflowStages.Screen);
        result.Rationale.Should().Contain("4 proposal");
    }

    [Fact]
    public void ValidationAlreadyRan_ResumesAtValidate()
    {
        // A project that reached the gate has been through matching. Re-running
        // matching would recompute a shortlist the client may already have acted
        // on.
        var result = StageResolver.Resolve(hasProposals: true, hasShortlist: true, hasValidation: true, proposalCount: 3, shortlistCount: 5);

        result.Stage.Should().Be(WorkflowStages.Validate);
        result.Rationale.Should().Contain("shortlist of 5");
    }

    [Fact]
    public void ShortlistWithoutValidation_GoesBackToTalentMatch()
    {
        // Deliberate, and the subtle case. A shortlist with no validation result
        // means the run is mid-pipeline, parked at the client gate -- so the
        // pipeline it belongs to is talent matching, not screening.
        //
        // Returning `validate` here would skip the gate the client has not yet
        // passed, and the agent would invite people nobody approved.
        var result = StageResolver.Resolve(hasProposals: false, hasShortlist: true, hasValidation: false, shortlistCount: 5);

        result.Stage.Should().Be(WorkflowStages.TalentMatch);
    }

    [Fact]
    public void Evidence_IsCarriedThrough()
    {
        // The counts come back with the decision because a wrong answer is only
        // diagnosable with the numbers that produced it.
        var result = StageResolver.Resolve(
            hasProposals: true, hasShortlist: true, hasValidation: true,
            proposalCount: 7, shortlistCount: 12);

        result.HasProposals.Should().BeTrue();
        result.HasShortlist.Should().BeTrue();
        result.HasValidation.Should().BeTrue();
        result.ProposalCount.Should().Be(7);
        result.ShortlistCount.Should().Be(12);
    }

    [Fact]
    public void EveryCombination_ResolvesToAKnownStage()
    {
        // Exhaustive over the three booleans: whatever the database says, the
        // answer has to be something the pipeline can actually run.
        foreach (var proposals in new[] { false, true })
        foreach (var shortlist in new[] { false, true })
        foreach (var validation in new[] { false, true })
        {
            var stage = StageResolver.Resolve(proposals, shortlist, validation).Stage;
            WorkflowStages.IsKnown(stage).Should().BeTrue(
                $"proposals={proposals} shortlist={shortlist} validation={validation} resolved to '{stage}'");
        }
    }
}
