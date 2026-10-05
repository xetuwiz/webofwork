using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebOfWork.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Proposals;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IProposalService
{
    Task<ProposalResponse> CreateProposalAsync(Guid userId, CreateProposalRequest request);
    Task<List<ProposalResponse>> GetMyProposalsAsync(Guid userId);
    Task<List<ProposalResponse>> GetProjectProposalsAsync(Guid userId, int projectId);
    Task<WebOfWork.Api.DTOs.Common.PagedResponse<ProposalResponse>> GetPagedProjectProposalsAsync(Guid userId, int projectId, ProposalStatus? status, string? search, int page, int pageSize);
    Task<bool> RejectProposalAsync(Guid userId, int proposalId);
    Task<bool> WithdrawProposalAsync(Guid userId, int proposalId);
}

public class ProposalService : IProposalService
{
    private readonly WebOfWorkDbContext _db;
    private readonly INotificationPublisher? _notificationPublisher;

    /// <summary>
    /// Starts the screening stage when a proposal arrives.
    /// </summary>
    /// <remarks>
    /// Optional, and the parameter order puts it last so existing construction
    /// sites and tests are unaffected. Before this existed nothing started a
    /// `screen` run, so the screening agent was reachable only by hand and
    /// `proposal_screenings` never gained a row from a real flow.
    /// </remarks>
    private readonly IAiWorkflowTrigger? _aiTrigger;

    public ProposalService(
        WebOfWorkDbContext db,
        INotificationPublisher? notificationPublisher = null,
        IAiWorkflowTrigger? aiTrigger = null)
    {
        _db = db;
        _notificationPublisher = notificationPublisher;
        _aiTrigger = aiTrigger;
    }

    private async Task<Guid> GetFreelancerIdAsync(Guid userId)
    {
        var profile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Freelancer profile not found.");
        return profile.Id;
    }

    private async Task<Guid> GetClientIdAsync(Guid userId)
    {
        var profile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Client profile not found.");
        return profile.Id;
    }

    public async Task<ProposalResponse> CreateProposalAsync(Guid userId, CreateProposalRequest request)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);

        // Identify a client by the user flag, not by the presence of a
        // client_profiles row. A freelancer can also act as a client, so they
        // legitimately have both profile rows -- testing for the client row
        // alone classified every freelancer as a client and blocked all
        // proposals with 401 "Clients cannot submit proposals."
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new KeyNotFoundException("User not found.");
        var isClient = !user.IsFreelancer;
        if (isClient) throw new UnauthorizedAccessException("Clients cannot submit proposals.");

        var projectId = request.Project != 0 ? request.Project : request.ProjectId;
        var packageId = request.Package != Guid.Empty ? request.Package : request.PackageId;
        var package = await _db.FreelancerPackages.FirstOrDefaultAsync(p => p.Id == packageId && p.UserId == userId);
        if (package == null) throw new ArgumentException("Invalid package or you do not own it.");

        // Only block if an active (Pending or Accepted) proposal exists.
        // Withdrawn or Rejected proposals allow re-submission.
        var existing = await _db.Proposals.AnyAsync(p =>
            p.ProjectId == projectId &&
            p.FreelancerId == freelancerId &&
            (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.Accepted));
        if (existing) throw new InvalidOperationException("You have already submitted a proposal for this project.");

        var invitationExists = await _db.Invitations.AnyAsync(i =>
            i.ProjectId == projectId &&
            i.FreelancerId == freelancerId &&
            (i.Status == InvitationStatus.Pending || i.Status == InvitationStatus.Accepted));
        if (invitationExists) throw new InvalidOperationException("An active invitation already exists for this project.");

        // The request surface only instructs clients to bid on open work, and the
        // front ends gate on is_active / status, but the DTO itself was never
        // guarded: a hand-built POST could bid on a Draft, Completed or cancelled
        // project, or on a project the freelancer posted themselves while acting
        // as a client. Enforce both here so the guard does not depend on the UI.
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new KeyNotFoundException("Project not found.");
        if (project.Status != ProjectStatus.Open) throw new InvalidOperationException("You can only bid on projects that are open.");
        if (!project.IsActive) throw new InvalidOperationException("This project is no longer accepting proposals.");
        var bidsOnOwnProject = await _db.ClientProfiles.AnyAsync(c => c.UserId == userId && c.Id == project.ClientId);
        if (bidsOnOwnProject) throw new InvalidOperationException("You cannot bid on your own project.");

        var proposal = new Proposal
        {
            ProjectId = projectId,
            FreelancerId = freelancerId,
            PackageId = packageId,
            CoverLetter = request.CoverLetter,
            BidAmount = package.Price,
            DeliveryDays = package.DeliveryDays,
            Status = ProposalStatus.Pending
        };

        _db.Proposals.Add(proposal);
        await _db.SaveChangesAsync();

        // Screening the proposals is a consequence of the proposal existing, not
        // something the freelancer asked for -- so it happens after the commit,
        // is not awaited, and cannot fail the submission.
        //
        // Requested by the *client's* user id rather than the freelancer's,
        // because the run belongs to the client's decision about who to hire, and
        // the admin monitor and the project's `by-project` lookup are both scoped
        // to `RequestedByUserId`. A run attributed to the freelancer would be
        // invisible to the client who is meant to act on it.
        // Temporarily disabled per user request: Do not trigger the AI Screening Agent automatically.
        /*
        if (_aiTrigger is not null)
        {
            var clientUserId = await _db.ClientProfiles
                .Where(c => c.Id == project.ClientId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (clientUserId != Guid.Empty)
                await _aiTrigger.ScreenProposalsAsync(projectId, clientUserId);
        }
        */

        return await GetProposalResponseAsync(proposal.Id);
    }

    public async Task<List<ProposalResponse>> GetMyProposalsAsync(Guid userId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);

        var proposals = await _db.Proposals.Include(p => p.Project).ThenInclude(p => p.Client).ThenInclude(c => c.User).Include(p => p.Freelancer).ThenInclude(f => f.User).Where(p => p.FreelancerId == freelancerId).OrderByDescending(p => p.CreatedAt).ToListAsync();

        return await MapWithContracts(proposals);

        async Task<List<ProposalResponse>> MapWithContracts(List<Proposal> list)
        {
            var map = await LoadContractInfoAsync(list);
            return list.Select(p => MapToResponse(p, map.GetValueOrDefault((p.ProjectId, p.FreelancerId)))).ToList();
        }
    }

    public async Task<List<ProposalResponse>> GetProjectProposalsAsync(Guid userId, int projectId)
    {
        var clientId = await GetClientIdAsync(userId);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.ClientId == clientId);
        if (project == null) throw new UnauthorizedAccessException("You do not own this project.");

        var proposals = await _db.Proposals.Include(p => p.Project).ThenInclude(p => p.Client).ThenInclude(c => c.User).Include(p => p.Freelancer).ThenInclude(f => f.User).Where(p => p.ProjectId == projectId).OrderByDescending(p => p.CreatedAt).ToListAsync();

        var projectContracts = await LoadContractInfoAsync(proposals);
        return proposals.Select(p => MapToResponse(p, projectContracts.GetValueOrDefault((p.ProjectId, p.FreelancerId)))).ToList();
    }

    public async Task<WebOfWork.Api.DTOs.Common.PagedResponse<ProposalResponse>> GetPagedProjectProposalsAsync(Guid userId, int projectId, ProposalStatus? status, string? search, int page, int pageSize)
    {
        var clientId = await GetClientIdAsync(userId);
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId && p.ClientId == clientId)) throw new UnauthorizedAccessException("You do not own this project.");
        var query = _db.Proposals.Include(p => p.Project).ThenInclude(p => p.Client).ThenInclude(c => c.User).Include(p => p.Freelancer).ThenInclude(f => f.User).Where(p => p.ProjectId == projectId).AsQueryable();
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.CoverLetter.Contains(search) || p.Project.Title.Contains(search));
        var normalizedPage = Math.Max(page, 1); var normalizedSize = Math.Clamp(pageSize, 1, 100);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt).Skip((normalizedPage - 1) * normalizedSize).Take(normalizedSize).ToListAsync();
        var pagedMap = await LoadContractInfoAsync(items);
        return new WebOfWork.Api.DTOs.Common.PagedResponse<ProposalResponse> { Results = items.Select(p => MapToResponse(p, pagedMap.GetValueOrDefault((p.ProjectId, p.FreelancerId)))).ToList(), Count = count, Page = normalizedPage, PageSize = normalizedSize };
    }

    /// <summary>
    /// The contract for each (project, freelancer) pair, if one exists. the reference
    /// ProposalsListSerializer.get_contract_info filters on BOTH project and
    /// freelancer, so contract_info is per proposal -- not per project. When a
    /// match exists is_this_freelancer is always true; otherwise null.
    /// </summary>
    private async Task<Dictionary<(int ProjectId, Guid FreelancerId), ProposalContractInfo>> LoadContractInfoAsync(IEnumerable<Proposal> proposals)
    {
        var list = proposals as IList<Proposal> ?? proposals.ToList();
        if (list.Count == 0) return new Dictionary<(int, Guid), ProposalContractInfo>();

        var projectIds = list.Select(p => p.ProjectId).Distinct().ToList();
        var freelancerIds = list.Select(p => p.FreelancerId).Distinct().ToList();

        var contracts = await _db.Contracts.AsNoTracking().Where(c => projectIds.Contains(c.ProjectId) && freelancerIds.Contains(c.FreelancerId)).OrderBy(c => c.Id).ToListAsync();

        return contracts.GroupBy(c => (c.ProjectId, c.FreelancerId)).ToDictionary(
                g => g.Key,
                g => new ProposalContractInfo
                {
                    Id = g.Last().Id,
                    Status = WebOfWork.Api.Helpers.SnakeCase.Of(g.Last().Status),
                    IsThisFreelancer = true
                });
    }

    public async Task<bool> RejectProposalAsync(Guid userId, int proposalId)
    {
        var clientId = await GetClientIdAsync(userId);

        var proposal = await _db.Proposals.Include(p => p.Project).Include(p => p.Freelancer).FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) return false;
        if (proposal.Project.ClientId != clientId) throw new UnauthorizedAccessException("You do not own the project for this proposal.");

        if (proposal.Status != ProposalStatus.Pending) throw new InvalidOperationException("Only pending proposals can be rejected.");

        proposal.Status = ProposalStatus.Rejected;
        await _db.SaveChangesAsync();
        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                proposal.Freelancer.UserId,
                NotificationType.ProposalRejected,
                $"Your proposal for project '{proposal.Project.Title}' was rejected.",
                "Proposal",
                proposal.Id);
        }
        return true;
    }

    public async Task<bool> WithdrawProposalAsync(Guid userId, int proposalId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);

        var proposal = await _db.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId);
        if (proposal == null) return false;
        if (proposal.FreelancerId != freelancerId) throw new UnauthorizedAccessException("You do not own this proposal.");

        if (proposal.Status != ProposalStatus.Pending) throw new InvalidOperationException("Only pending proposals can be withdrawn.");

        proposal.Status = ProposalStatus.Withdrawn;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<ProposalResponse> GetProposalResponseAsync(int proposalId)
    {
        var proposal = await _db.Proposals.Include(p => p.Project).ThenInclude(p => p.Client).ThenInclude(c => c.User).Include(p => p.Freelancer).ThenInclude(f => f.User).FirstOrDefaultAsync(p => p.Id == proposalId);

        return MapToResponse(proposal!);
    }

    private ProposalResponse MapToResponse(Proposal proposal, ProposalContractInfo? contractInfo = null)
    {
        return new ProposalResponse
        {
            Id = proposal.Id,
            ProjectId = proposal.ProjectId,
            ProjectTitle = proposal.Project?.Title ?? string.Empty,
            FreelancerId = proposal.FreelancerId,
            FreelancerName = proposal.Freelancer?.User?.FullName ?? string.Empty,
            FreelancerPhoto = proposal.Freelancer?.User?.ProfilePhoto ?? string.Empty,
            FreelancerTagline = proposal.Freelancer?.Tagline ?? string.Empty,
            FreelancerRatings = proposal.Freelancer?.AverageRating ?? 0m,
            ClientName = proposal.Project?.Client?.User?.FullName ?? string.Empty,
            PackageId = proposal.PackageId,
            CoverLetter = proposal.CoverLetter,
            BidAmount = proposal.BidAmount,
            DeliveryDays = proposal.DeliveryDays,
            Status = proposal.Status,
            CreatedAt = proposal.CreatedAt,
            ContractInfo = contractInfo == null ? null : new ProposalContractInfo
            {
                Id = contractInfo.Id,
                Status = contractInfo.Status,
                IsThisFreelancer = contractInfo.IsThisFreelancer
            }
        };
    }
}
