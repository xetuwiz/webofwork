using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/ai/workflows")]
[Authorize]
public class AiWorkflowController : ControllerBase
{
    private readonly IAiWorkflowService _aiWorkflowService;
    private readonly ICurrentUser _currentUser;

    public AiWorkflowController(IAiWorkflowService aiWorkflowService, ICurrentUser currentUser)
    {
        _aiWorkflowService = aiWorkflowService;
        _currentUser = currentUser;
    }

    [HttpPost("start")]
    [Authorize(Policy = AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> StartWorkflow([FromBody] StartWorkflowRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated client user is required.");
        var run = await _aiWorkflowService.StartWorkflowAsync(userId, request.Objective, request.Plan, request.ProjectId, cancellationToken);
        return Ok(run);
    }

    [HttpPost("{workflowRunId:int}/approve")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> ApproveWorkflow(int workflowRunId, [FromBody] ApproveWorkflowRequest? request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated admin user is required.");
        var contractSpec = request?.FreelancerId is { } freelancerId && request.TotalAmount is { } totalAmount
            ? new WorkflowContractSpec(freelancerId, totalAmount, request.PackageId, request.ProjectId)
            : null;
        var run = await _aiWorkflowService.ApproveWorkflowAsync(workflowRunId, userId, request?.Decision, contractSpec, cancellationToken);
        return Ok(run);
    }

    [HttpPost("{workflowRunId:int}/cancel")]
    public async Task<IActionResult> CancelWorkflow(int workflowRunId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated user is required.");
        var run = await _aiWorkflowService.CancelWorkflowAsync(workflowRunId, userId, cancellationToken);
        return Ok(run);
    }
}

public sealed class StartWorkflowRequest
{
    public string Objective { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public int? ProjectId { get; set; }
}

public sealed class ApproveWorkflowRequest
{
    public string? Decision { get; set; }
    public Guid? FreelancerId { get; set; }
    public decimal? TotalAmount { get; set; }
    public Guid? PackageId { get; set; }
    public int? ProjectId { get; set; }
}
