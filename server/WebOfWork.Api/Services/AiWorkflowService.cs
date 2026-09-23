using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net.Http.Json;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IAiWorkflowService
{
    Task<WorkflowRun> StartWorkflowAsync(Guid requestedByUserId, string objective, string? plan = null, int? projectId = null, CancellationToken ct = default);
    Task<WorkflowRun> ApproveWorkflowAsync(int workflowRunId, Guid approvedByUserId, string? decision = null, WorkflowContractSpec? contractSpec = null, CancellationToken ct = default);
    Task<WorkflowRun> CancelWorkflowAsync(int workflowRunId, Guid requestedByUserId, CancellationToken ct = default);
    Task<WorkflowStepLog> AppendStepLogAsync(int workflowRunId, string stepName, string? agentName, string? input, string? output, string status, long durationMs, CancellationToken ct = default);
    Task<ToolCallLog> AppendToolCallLogAsync(int workflowRunId, string toolName, string? input, string? output, bool success, long durationMs, CancellationToken ct = default);
    Task<AgentRecommendation> AppendAgentRecommendationAsync(int workflowRunId, AgentRecommendation recommendation, CancellationToken ct = default);
    Task<ProposalScreening> AppendProposalScreeningAsync(int workflowRunId, ProposalScreening screening, CancellationToken ct = default);
    Task<ValidationResult> AppendValidationResultAsync(int workflowRunId, ValidationResult result, CancellationToken ct = default);
    Task<WorkflowRun> CompleteWorkflowAsync(int workflowRunId, string? output, CancellationToken ct = default);
}

public sealed record WorkflowContractSpec(
    Guid FreelancerId,
    decimal TotalAmount,
    Guid? PackageId = null,
    bool IsMilestoneBased = false,
    int MilestoneCount = 0,
    int? ProjectId = null);

public class AiWorkflowService : IAiWorkflowService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IBackgroundJobClient _jobs;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly AgenticAiOptions? _aiOptions;

    private static string? SerializeJsonbString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(value);
        }
    }

    public AiWorkflowService(
        WebOfWorkDbContext db,
        IBackgroundJobClient jobs,
        IHttpClientFactory? httpClientFactory = null,
        IOptions<AgenticAiOptions>? aiOptions = null)
    {
        _db = db;
        _jobs = jobs;
        _httpClientFactory = httpClientFactory;
        _aiOptions = aiOptions?.Value;
    }

    public async Task<WorkflowRun> StartWorkflowAsync(Guid requestedByUserId, string objective, string? plan = null, int? projectId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objective))
            throw new ArgumentException("Workflow objective is required.", nameof(objective));

        var run = new WorkflowRun
        {
            Objective = objective.Trim(),
            Plan = SerializeJsonbString(plan),
            ProjectId = projectId,
            RequestedByUserId = requestedByUserId,
            Status = WorkflowRunStatus.Pending,
            ApprovalDecision = "Pending",
            FinalOutput = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.WorkflowRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var adminIds = await _db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Admin && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var adminId in adminIds)
        {
            _jobs.Enqueue<WebOfWork.Api.Jobs.PublishNotificationJob>(job => job.ExecuteAsync(
                adminId,
                $"Workflow #{run.Id} is awaiting admin approval.",
                NotificationType.WorkflowApprovalRequired,
                CancellationToken.None));
        }

        await TryStartPythonAgentAsync(run, ct);
        return run;
    }

    public async Task<WorkflowRun> ApproveWorkflowAsync(int workflowRunId, Guid approvedByUserId, string? decision = null, WorkflowContractSpec? contractSpec = null, CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        var approvalDecision = string.IsNullOrWhiteSpace(decision) ? "Approved" : decision.Trim();
        var rejected = string.Equals(approvalDecision, "Rejected", StringComparison.OrdinalIgnoreCase);
        run.Status = rejected ? WorkflowRunStatus.Rejected : WorkflowRunStatus.Approved;
        run.ApprovalDecision = approvalDecision;
        run.ApprovedByUserId = approvedByUserId;
        run.ApprovedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.FinalOutput ??= rejected ? "Rejected by admin." : "Approved by admin and ready for execution.";

        if (!rejected)
        {
            contractSpec ??= TryReadContractSpec(run.Plan);
            if (contractSpec is not null)
            {
                var projectId = contractSpec.ProjectId ?? run.ProjectId
                    ?? throw new InvalidOperationException("A project is required to create the approved contract.");
                if (run.ProjectId.HasValue && run.ProjectId.Value != projectId)
                    throw new InvalidOperationException("The approval project does not match the workflow project.");
                if (contractSpec.TotalAmount <= 0)
                    throw new InvalidOperationException("The approved contract amount must be greater than zero.");

                var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, ct)
                    ?? throw new KeyNotFoundException($"Project {projectId} was not found.");
                var freelancer = await _db.FreelancerProfiles.FirstOrDefaultAsync(x => x.Id == contractSpec.FreelancerId, ct)
                    ?? throw new KeyNotFoundException($"Freelancer {contractSpec.FreelancerId} was not found.");

                var contract = new Contract
                {
                    ProjectId = project.Id,
                    ClientId = project.ClientId,
                    FreelancerId = freelancer.Id,
                    PackageId = contractSpec.PackageId,
                    TotalAmount = contractSpec.TotalAmount,
                    IsMilestoneBased = contractSpec.IsMilestoneBased,
                    MilestoneCount = contractSpec.MilestoneCount,
                    ClientSignature = "AI-Workflow-Approval",
                    ClientSignatureType = "workflow",
                    Status = ContractStatus.Offered,
                    ClientSignedAt = DateTime.UtcNow
                };
                _db.Contracts.Add(contract);
                await _db.SaveChangesAsync(ct);

                _jobs.Enqueue<WebOfWork.Api.Jobs.ContractOfferNotificationJob>(job => job.ExecuteAsync(
                    freelancer.UserId, contract.Id, contract.TotalAmount, CancellationToken.None));
            }
        }

        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<WorkflowRun> CancelWorkflowAsync(int workflowRunId, Guid requestedByUserId, CancellationToken ct = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        if (run.RequestedByUserId != requestedByUserId)
            throw new UnauthorizedAccessException("Only the requesting user can cancel this workflow.");

        if (run.Status != WorkflowRunStatus.Pending && run.Status != WorkflowRunStatus.Running)
            throw new InvalidOperationException("Only pending or running workflows can be cancelled.");

        run.Status = WorkflowRunStatus.Cancelled;
        run.ApprovalDecision = "Cancelled";
        run.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<WorkflowStepLog> AppendStepLogAsync(int workflowRunId, string stepName, string? agentName, string? input, string? output, string status, long durationMs, CancellationToken ct = default)
    {
        var workflow = await _db.WorkflowRuns
            .FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

        var log = new WorkflowStepLog
        {
            WorkflowRunId = workflow.Id,
            StepName = stepName,
            AgentName = agentName,
            Input = SerializeJsonbString(input),
            Output = SerializeJsonbString(output),
            Status = status,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkflowStepLogs.Add(log);
        ApplyStepStatus(workflow, status);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<ToolCallLog> AppendToolCallLogAsync(int workflowRunId, string toolName, string? input, string? output, bool success, long durationMs, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        var log = new ToolCallLog
        {
            WorkflowRunId = workflowRunId,
            ToolName = toolName,
            Input = SerializeJsonbString(input),
            Output = SerializeJsonbString(output),
            Success = success,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };
        _db.ToolCallLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<AgentRecommendation> AppendAgentRecommendationAsync(int workflowRunId, AgentRecommendation recommendation, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        recommendation.WorkflowRunId = workflowRunId;
        recommendation.FactorsJson = SerializeJsonbString(recommendation.FactorsJson);
        recommendation.PortfolioSamplesJson = SerializeJsonbString(recommendation.PortfolioSamplesJson);
        _db.AgentRecommendations.Add(recommendation);
        await _db.SaveChangesAsync(ct);
        return recommendation;
    }

    public async Task<ProposalScreening> AppendProposalScreeningAsync(int workflowRunId, ProposalScreening screening, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        screening.WorkflowRunId = workflowRunId;
        screening.StrengthsJson = SerializeJsonbString(screening.StrengthsJson);
        screening.ConcernsJson = SerializeJsonbString(screening.ConcernsJson);
        _db.ProposalScreenings.Add(screening);
        await _db.SaveChangesAsync(ct);
        return screening;
    }

    public async Task<ValidationResult> AppendValidationResultAsync(int workflowRunId, ValidationResult result, CancellationToken ct = default)
    {
        await EnsureWorkflowAsync(workflowRunId, ct);
        result.WorkflowRunId = workflowRunId;
        result.IssuesJson = SerializeJsonbString(result.IssuesJson);
        result.CostBreakdownJson = SerializeJsonbString(result.CostBreakdownJson);
        _db.ValidationResults.Add(result);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<WorkflowRun> CompleteWorkflowAsync(int workflowRunId, string? output, CancellationToken ct = default)
    {
        var workflow = await _db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");
        workflow.Status = WorkflowRunStatus.Completed;
        workflow.FinalOutput = output;
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return workflow;
    }

    private async Task<WorkflowRun> EnsureWorkflowAsync(int workflowRunId, CancellationToken ct)
        => await _db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, ct)
            ?? throw new KeyNotFoundException($"Workflow run {workflowRunId} was not found.");

    private static void ApplyStepStatus(WorkflowRun workflow, string status)
    {
        if (Enum.TryParse<WorkflowRunStatus>(status, true, out var parsed))
            workflow.Status = parsed;
    }

    private static WorkflowContractSpec? TryReadContractSpec(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan)) return null;
        try
        {
            using var document = JsonDocument.Parse(plan);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.Deserialize<WorkflowContractSpec>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task TryStartPythonAgentAsync(WorkflowRun run, CancellationToken ct)
    {
        if (_httpClientFactory is null || _aiOptions is null || string.IsNullOrWhiteSpace(_aiOptions.BaseUrl))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("AgenticAi");
            using var request = new HttpRequestMessage(HttpMethod.Post, "agent/chat")
            {
                Content = JsonContent.Create(new
                {
                    message = run.Objective,
                    execution_mode = "recorded_replay",
                    workflow_run_id = run.Id
                })
            };
            if (!string.IsNullOrWhiteSpace(_aiOptions.InternalApiKey))
                request.Headers.Add("X-Internal-Api-Key", _aiOptions.InternalApiKey);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return;

            run.Status = WorkflowRunStatus.PendingApproval;
            run.FinalOutput = await response.Content.ReadAsStringAsync(ct);
            run.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (HttpRequestException)
        {
            // The workflow remains persisted and can be completed through the internal callback path.
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // The bounded AI call timed out; leave the durable workflow pending.
        }
    }
}
