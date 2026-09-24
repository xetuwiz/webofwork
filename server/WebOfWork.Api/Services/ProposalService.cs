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

    public ProposalService(WebOfWorkDbContext db, INotificationPublisher? notificationPublisher = null)
    {
        _db = db;
        _notificationPublisher = notificationPublisher;
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

        var isClient = await _db.ClientProfiles.AnyAsync(c => c.UserId == userId);
        if (isClient) throw new UnauthorizedAccessException("Clients cannot submit proposals.");

        var package = await _db.FreelancerPackages.FirstOrDefaultAsync(p => p.Id == request.PackageId && p.UserId == userId);
        if (package == null) throw new ArgumentException("Invalid package or you do not own it.");

        // Only block if an active (Pending or Accepted) proposal exists.
        // Withdrawn or Rejected proposals allow re-submission.
        var existing = await _db.Proposals.AnyAsync(p =>
            p.ProjectId == request.ProjectId &&
            p.FreelancerId == freelancerId &&
            (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.Accepted));
        if (existing) throw new InvalidOperationException("You have already submitted a proposal for this project.");

        var invitationExists = await _db.Invitations.AnyAsync(i =>
            i.ProjectId == request.ProjectId &&
            i.FreelancerId == freelancerId &&
            (i.Status == InvitationStatus.Pending || i.Status == InvitationStatus.Accepted));
        if (invitationExists) throw new InvalidOperationException("An active invitation already exists for this project.");

        var proposal = new Proposal
        {
            ProjectId = request.ProjectId,
            FreelancerId = freelancerId,
            PackageId = request.PackageId,
            CoverLetter = request.CoverLetter,
            BidAmount = request.BidAmount,
            DeliveryDays = request.DeliveryDays,
            Status = ProposalStatus.Pending
        };

        _db.Proposals.Add(proposal);
        await _db.SaveChangesAsync();

        return await GetProposalResponseAsync(proposal.Id);
    }

    public async Task<List<ProposalResponse>> GetMyProposalsAsync(Guid userId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);

        var proposals = await _db.Proposals
            .Include(p => p.Project)
            .Include(p => p.Freelancer)
            .ThenInclude(f => f.User)
            .Where(p => p.FreelancerId == freelancerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return proposals.Select(MapToResponse).ToList();
    }

    public async Task<List<ProposalResponse>> GetProjectProposalsAsync(Guid userId, int projectId)
    {
        var clientId = await GetClientIdAsync(userId);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.ClientId == clientId);
        if (project == null) throw new UnauthorizedAccessException("You do not own this project.");

        var proposals = await _db.Proposals
            .Include(p => p.Project)
            .Include(p => p.Freelancer)
            .ThenInclude(f => f.User)
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return proposals.Select(MapToResponse).ToList();
    }

    public async Task<WebOfWork.Api.DTOs.Common.PagedResponse<ProposalResponse>> GetPagedProjectProposalsAsync(Guid userId, int projectId, ProposalStatus? status, string? search, int page, int pageSize)
    {
        var clientId = await GetClientIdAsync(userId);
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId && p.ClientId == clientId)) throw new UnauthorizedAccessException("You do not own this project.");
        var query = _db.Proposals.Include(p => p.Project).Include(p => p.Freelancer).ThenInclude(f => f.User).Where(p => p.ProjectId == projectId).AsQueryable();
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.CoverLetter.Contains(search) || p.Project.Title.Contains(search));
        var normalizedPage = Math.Max(page, 1); var normalizedSize = Math.Clamp(pageSize, 1, 100);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt).Skip((normalizedPage - 1) * normalizedSize).Take(normalizedSize).ToListAsync();
        return new WebOfWork.Api.DTOs.Common.PagedResponse<ProposalResponse> { Results = items.Select(MapToResponse).ToList(), Count = count, Page = normalizedPage, PageSize = normalizedSize };
    }

    public async Task<bool> RejectProposalAsync(Guid userId, int proposalId)
    {
        var clientId = await GetClientIdAsync(userId);

        var proposal = await _db.Proposals
            .Include(p => p.Project)
            .Include(p => p.Freelancer)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

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
        var proposal = await _db.Proposals
            .Include(p => p.Project)
            .Include(p => p.Freelancer)
            .ThenInclude(f => f.User)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        return MapToResponse(proposal!);
    }

    private ProposalResponse MapToResponse(Proposal proposal)
    {
        return new ProposalResponse
        {
            Id = proposal.Id,
            ProjectId = proposal.ProjectId,
            ProjectTitle = proposal.Project?.Title ?? string.Empty,
            FreelancerId = proposal.FreelancerId,
            FreelancerName = proposal.Freelancer?.User?.FullName ?? string.Empty,
            PackageId = proposal.PackageId,
            CoverLetter = proposal.CoverLetter,
            BidAmount = proposal.BidAmount,
            DeliveryDays = proposal.DeliveryDays,
            Status = proposal.Status.ToString(),
            CreatedAt = proposal.CreatedAt
        };
    }
}
