using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Services;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/ai/workflows")]
[Authorize]
public class AiWorkflowController : ControllerBase
{
    private readonly IAiWorkflowService _aiWorkflowService;
    private readonly ICurrentUser _currentUser;
    private readonly WebOfWorkDbContext _db;

    public AiWorkflowController(IAiWorkflowService aiWorkflowService, ICurrentUser currentUser, WebOfWorkDbContext db)
    {
        _aiWorkflowService = aiWorkflowService;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> ListWorkflows([FromQuery] WorkflowRunStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var query = _db.WorkflowRuns.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100)).ToListAsync(cancellationToken);
        return Ok(new { results = items, count = total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)Math.Clamp(pageSize, 1, 100)) });
    }

    [HttpGet("{workflowRunId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> GetWorkflow(int workflowRunId, CancellationToken cancellationToken)
    {
        var run = await _db.WorkflowRuns.AsNoTracking().Include(x => x.Project).Include(x => x.StepLogs).FirstOrDefaultAsync(x => x.Id == workflowRunId, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("by-project/{projectId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireClient)]
    public async Task<IActionResult> GetProjectWorkflowStatus(int projectId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Authenticated client user is required.");
        var run = await _db.WorkflowRuns.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.RequestedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.ProjectId, x.Status, x.Objective, x.ApprovalDecision, x.ApprovalRationale, x.CreatedAt, x.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        return run is null ? NotFound() : Ok(run);
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
        var run = await _aiWorkflowService.ApproveWorkflowAsync(workflowRunId, userId, request?.Decision, request?.Rationale, contractSpec, cancellationToken);
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
    public string? Rationale { get; set; }
    public Guid? FreelancerId { get; set; }
    public decimal? TotalAmount { get; set; }
    public Guid? PackageId { get; set; }
    public int? ProjectId { get; set; }
}
