using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using WebOfWork.Api.Models;
using Xunit;

namespace WebOfWork.Api.Tests.Ai;

/// <summary>
/// A run with its step logs must be serializable.
/// </summary>
/// <remarks>
/// <para>
/// The shortlist page could not display a run's status at all, and the cause was
/// not visible from the endpoint's own log line.
/// </para>
/// <para>
/// <c>WorkflowRun.StepLogs</c> is a collection of <c>WorkflowStepLog</c>, and each
/// of those has a <c>WorkflowRun</c> navigation property pointing back at its
/// parent. That is a cycle, and <c>System.Text.Json</c> refuses to serialize one:
/// it throws <c>JsonException: A possible object cycle was detected</c> once the
/// graph passes the depth limit of 32. The client then received a truncated body
/// and reported a failed load.
/// </para>
/// <para>
/// It stayed hidden because two bugs were stacked. The endpoint returned 403 to
/// clients before serialization was ever reached, so the cycle was unreachable.
/// Fixing the authorization exposed it on the very next request. A test that
/// only asserted the status code would have passed in both states -- which is
/// why these assert that the bytes come out whole.
/// </para>
/// </remarks>
public class WorkflowRunSerializationTests
{
    /// <summary>
    /// The same options MVC uses, so the test serializes the way the endpoint does.
    /// </summary>
    /// <remarks>
    /// `JsonSerializerDefaults.Web` means camelCase on the wire, which is why the
    /// assertions below look for `stepLogs` and not `StepLogs`. Asserting the
    /// property names the API actually emits is deliberate: a test that used the
    /// C# spelling would fail on a naming-policy change for a reason unrelated to
    /// the cycle, and a test that avoided naming them could not tell an empty
    /// `stepLogs` from a missing one.
    /// </remarks>
    private static JsonSerializerOptions Options => new(JsonSerializerDefaults.Web);

    /// <summary>A run shaped exactly as EF materialises it with its logs loaded.</summary>
    private static WorkflowRun RunWithStepLogs(int logCount = 3)
    {
        var run = new WorkflowRun
        {
            Id = 23,
            Objective = "Find talent for a responsive website",
            Stage = WorkflowStages.TalentMatch,
            Status = WorkflowRunStatus.PendingClientApproval,
            ProjectId = 11,
        };

        for (var i = 0; i < logCount; i++)
        {
            run.StepLogs.Add(new WorkflowStepLog
            {
                Id = i + 1,
                WorkflowRunId = run.Id,
                WorkflowRun = run,
                StepName = $"step-{i}",
                AgentName = "planning",
                Status = "Completed",
            });
        }

        return run;
    }

    [Fact]
    public void A_run_with_step_logs_serializes()
    {
        // The regression itself. Two logs is already enough to start the walk;
        // the depth limit is what turns a cycle into an exception rather than an
        // infinite loop, so a small graph still fails.
        var run = RunWithStepLogs();

        var act = () => JsonSerializer.Serialize(run, Options);

        act.Should().NotThrow();
    }

    [Fact]
    public void The_step_logs_are_still_present_after_the_fix()
    {
        // The cycle must be broken by dropping the back-reference, not by
        // dropping the logs. A shortlist page that cannot see the agent trajectory
        // has lost the thing the endpoint exists to return.
        var json = JsonSerializer.Serialize(RunWithStepLogs(logCount: 2), Options);

        using var document = JsonDocument.Parse(json);
        var stepLogs = document.RootElement.GetProperty("stepLogs");

        stepLogs.GetArrayLength().Should().Be(2);
        stepLogs[0].GetProperty("agentName").GetString().Should().Be("planning");
    }

    [Fact]
    public void The_correlating_id_is_present_even_though_the_object_is_not()
    {
        // `WorkflowRunId` is the field a client actually needs to join a log back
        // to its run, so losing the navigation property must not lose the id.
        var json = JsonSerializer.Serialize(RunWithStepLogs(logCount: 1), Options);

        using var document = JsonDocument.Parse(json);
        var firstLog = document.RootElement.GetProperty("stepLogs")[0];

        firstLog.GetProperty("workflowRunId").GetInt32().Should().Be(23);
        firstLog.TryGetProperty("workflowRun", out _).Should().BeFalse();
    }

    [Fact]
    public void A_deeply_nested_graph_still_serializes()
    {
        // Well past the default depth of 32, because a run accumulates a log per
        // agent step and a long workflow is not a small one.
        var json = JsonSerializer.Serialize(RunWithStepLogs(logCount: 40), Options);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("stepLogs").GetArrayLength().Should().Be(40);
    }

    [Fact]
    public void The_payload_does_not_grow_with_the_back_reference()
    {
        // `ReferenceHandler.Preserve` would also have stopped the throw, at the
        // cost of `$id`/`$ref` markers. This pins that the fix was the cheap one:
        // no markers, and a body that stays small.
        var json = JsonSerializer.Serialize(RunWithStepLogs(logCount: 40), Options);

        json.Should().NotContain("$id");
        json.Should().NotContain("$ref");
        json.Length.Should().BeLessThan(20_000);
    }
}
