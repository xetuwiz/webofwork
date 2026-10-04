using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("internal/workflows")]
public class InternalWorkflowController : ControllerBase
{
    private readonly IAiWorkflowService _aiWorkflowService;

    public InternalWorkflowController(IAiWorkflowService aiWorkflowService)
    {
        _aiWorkflowService = aiWorkflowService;
    }

    [HttpPut("{workflowRunId:int}/step-complete")]
    public async Task<IActionResult> CompleteStep(int workflowRunId, [FromBody] CompleteWorkflowStepRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var step = await _aiWorkflowService.AppendStepLogAsync(
            workflowRunId,
            request.StepName,
            request.AgentName,
            request.Input,
            request.Output,
            request.Status,
            request.DurationMs,
            cancellationToken);

        // A projection, not the entity.
        //
        // `WorkflowStepLog.WorkflowRun.StepLogs` points back at the collection
        // that contains it, so serialising the tracked entity walks
        // StepLogs -> WorkflowRun -> StepLogs -> ... and the system text serialiser
        // throws "a possible object cycle was detected" at depth 32. That is a 500
        // on a write that succeeded, so the agent service logged a failed write and
        // the step silently did not appear.
        //
        // The caller needs an id to correlate on and nothing else, so it gets
        // exactly that. Returning the entity was never useful here and cost a
        // round trip that failed.
        return Ok(new
        {
            id = step.Id,
            workflowRunId = step.WorkflowRunId,
            stepName = step.StepName,
            agentName = step.AgentName,
            status = step.Status,
            durationMs = step.DurationMs,
            createdAt = step.CreatedAt,
        });
    }

    [HttpPut("{workflowRunId:int}/complete")]
    public async Task<IActionResult> CompleteWorkflow(int workflowRunId, [FromBody] CompleteWorkflowStepRequest request, CancellationToken cancellationToken)
    {
        // The status is forwarded rather than assumed. `CompleteWorkflowAsync` used to
        // hardcode Completed, which is wrong for a stage that ends by waiting for a
        // person -- and `Start` de-duplicates on Pending/Running/PendingClientApproval,
        // so a paused run left at Pending was never re-dispatched.
        var workflow = await _aiWorkflowService.CompleteWorkflowAsync(
            workflowRunId, request.Output, request.Status, cancellationToken);

        // Projection again. `WorkflowRun.StepLogs` is a collection whose entities
        // each navigate back to the run, so serialising it cycles.
        return Ok(new
        {
            id = workflow.Id,
            status = workflow.Status.ToString(),
            stage = workflow.Stage,
            updatedAt = workflow.UpdatedAt,
        });
    }

    [HttpPut("{workflowRunId:int}/tool-call")]
    public async Task<IActionResult> CompleteToolCall(int workflowRunId, [FromBody] CompleteToolCallRequest request, CancellationToken cancellationToken)
    {
        // Projection for the same reason as `step-complete`: the entity's
        // `WorkflowRun.ToolCallLogs` navigation cycles back to itself and the
        // serialiser throws, turning a successful write into a 500.
        var log = await _aiWorkflowService.AppendToolCallLogAsync(
            workflowRunId, request.ToolName, request.Input, request.Output,
            request.Success, request.DurationMs, cancellationToken);

        return Ok(new
        {
            id = log.Id,
            workflowRunId = log.WorkflowRunId,
            toolName = log.ToolName,
            success = log.Success,
            durationMs = log.DurationMs,
        });
    }

    [HttpPut("{workflowRunId:int}/recommendation")]
    public async Task<IActionResult> AddRecommendation(int workflowRunId, [FromBody] AgentRecommendationRequest request, CancellationToken cancellationToken)
        => Ok(await _aiWorkflowService.AppendAgentRecommendationAsync(workflowRunId, new AgentRecommendation
        {
            ProjectId = request.ProjectId,
            FreelancerId = request.FreelancerId,
            Score = request.Score,
            FactorsJson = RecommendationFactors.Merge(
                request.FactorsJson, request.Strengths, request.Gaps),
            Reason = request.Reason,
            PortfolioSamplesJson = request.PortfolioSamplesJson
        }, cancellationToken));

    [HttpPut("{workflowRunId:int}/screening")]
    public async Task<IActionResult> AddScreening(int workflowRunId, [FromBody] ProposalScreeningRequest request, CancellationToken cancellationToken)
        => Ok(await _aiWorkflowService.AppendProposalScreeningAsync(workflowRunId, new ProposalScreening
        {
            ProposalId = request.ProposalId,
            FitScore = request.FitScore,
            StrengthsJson = request.StrengthsJson,
            ConcernsJson = request.ConcernsJson,
            Recommendation = request.Recommendation,
            Rationale = request.Rationale
        }, cancellationToken));

    [HttpPut("{workflowRunId:int}/validation")]
    public async Task<IActionResult> AddValidationResult(int workflowRunId, [FromBody] ValidationResultRequest request, CancellationToken cancellationToken)
        => Ok(await _aiWorkflowService.AppendValidationResultAsync(workflowRunId, new ValidationResult
        {
            ProjectId = request.ProjectId,
            ProposalId = request.ProposalId,
            FreelancerId = request.FreelancerId,
            Valid = request.Valid,
            IssuesJson = request.IssuesJson,
            CostBreakdownJson = request.CostBreakdownJson,
            RequiresHumanApproval = request.RequiresHumanApproval,
            HumanSummary = request.HumanSummary
        }, cancellationToken));
}

public sealed class CompleteWorkflowStepRequest
{
    public string StepName { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string Status { get; set; } = "Completed";
    public long DurationMs { get; set; }
}

public sealed class CompleteToolCallRequest
{
    public string ToolName { get; set; } = string.Empty;
    public string? Input { get; set; }
    public string? Output { get; set; }
    public bool Success { get; set; }
    public long DurationMs { get; set; }
}

public sealed class AgentRecommendationRequest
{
    public int ProjectId { get; set; }
    public Guid FreelancerId { get; set; }
    public decimal Score { get; set; }
    public string? FactorsJson { get; set; }
    public string? Reason { get; set; }
    public string? PortfolioSamplesJson { get; set; }

    /// <summary>
    /// Skills and evidence that counted in this candidate's favour.
    /// </summary>
    /// <remarks>
    /// The matching agent has always sent these. They were silently dropped,
    /// because this DTO had no field for them, which is why both the client's
    /// shortlist and the freelancer's invitation rendered a score and a sentence
    /// of reasoning with no supporting detail -- the two fields a reader
    /// actually uses to decide whether to trust the score.
    ///
    /// Folded into <see cref="AgentRecommendation.FactorsJson"/> on write rather
    /// than given columns of their own: the blob is already a free-form JSON
    /// document, so this needs no migration and every existing reader of the
    /// factors gets them without a second query.
    /// </remarks>
    public List<string>? Strengths { get; set; }

    /// <summary>Required skills this candidate does not have.</summary>
    public List<string>? Gaps { get; set; }
}

public sealed class ProposalScreeningRequest
{
    public int ProposalId { get; set; }
    public decimal FitScore { get; set; }
    public string? StrengthsJson { get; set; }
    public string? ConcernsJson { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string? Rationale { get; set; }
}

public sealed class ValidationResultRequest
{
    public int ProjectId { get; set; }
    public int ProposalId { get; set; }
    public Guid FreelancerId { get; set; }
    public bool Valid { get; set; }
    public string? IssuesJson { get; set; }
    public string? CostBreakdownJson { get; set; }
    public bool RequiresHumanApproval { get; set; }
    public string? HumanSummary { get; set; }
}
