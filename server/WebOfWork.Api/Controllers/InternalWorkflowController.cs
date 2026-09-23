using Microsoft.AspNetCore.Mvc;
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

        return Ok(step);
    }

    [HttpPut("{workflowRunId:int}/complete")]
    public async Task<IActionResult> CompleteWorkflow(int workflowRunId, [FromBody] CompleteWorkflowStepRequest request, CancellationToken cancellationToken)
    {
        var workflow = await _aiWorkflowService.CompleteWorkflowAsync(workflowRunId, request.Output, cancellationToken);
        return Ok(workflow);
    }

    [HttpPut("{workflowRunId:int}/tool-call")]
    public async Task<IActionResult> CompleteToolCall(int workflowRunId, [FromBody] CompleteToolCallRequest request, CancellationToken cancellationToken)
        => Ok(await _aiWorkflowService.AppendToolCallLogAsync(workflowRunId, request.ToolName, request.Input, request.Output, request.Success, request.DurationMs, cancellationToken));

    [HttpPut("{workflowRunId:int}/recommendation")]
    public async Task<IActionResult> AddRecommendation(int workflowRunId, [FromBody] AgentRecommendationRequest request, CancellationToken cancellationToken)
        => Ok(await _aiWorkflowService.AppendAgentRecommendationAsync(workflowRunId, new AgentRecommendation
        {
            ProjectId = request.ProjectId,
            FreelancerId = request.FreelancerId,
            Score = request.Score,
            FactorsJson = request.FactorsJson,
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
