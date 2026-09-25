using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.DTOs.Common;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IInvitationService
{
    Task<InvitationResponse> CreateInvitationAsync(Guid userId, CreateInvitationRequest request);
    Task<List<InvitationResponse>> GetSentInvitationsAsync(Guid userId);
    Task<List<InvitationResponse>> GetReceivedInvitationsAsync(Guid userId);
    Task<PagedResponse<InvitationResponse>> GetPagedSentInvitationsAsync(Guid userId, InvitationStatus? status, int page, int pageSize);
    Task<PagedResponse<InvitationResponse>> GetPagedReceivedInvitationsAsync(Guid userId, InvitationStatus? status, int page, int pageSize, bool closedOnly = false);
    Task<bool> UpdateInvitationStatusAsync(Guid userId, int invitationId, UpdateInvitationStatusRequest request);
    Task<bool> CancelInvitationAsync(Guid userId, int invitationId);
    Task<List<Project>> GetEligibleProjectsAsync(Guid userId); // For UI dropdowns
}

public class InvitationService : IInvitationService
{
    private readonly WebOfWorkDbContext _db;
    private readonly ICurrentUser? _currentUser;
    private readonly INotificationPublisher? _notificationPublisher;

    public InvitationService(WebOfWorkDbContext db, INotificationPublisher? notificationPublisher = null, ICurrentUser? currentUser = null)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationPublisher = notificationPublisher;
    }

    private async Task<Guid> GetClientIdAsync(Guid userId)
    {
        var profile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Client profile not found.");
        return profile.Id;
    }

    private async Task<Guid> GetFreelancerIdAsync(Guid userId)
    {
        var profile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Freelancer profile not found.");
        return profile.Id;
    }

    public async Task<InvitationResponse> CreateInvitationAsync(Guid userId, CreateInvitationRequest request)
    {
        var clientId = await GetClientIdAsync(userId);

        var freelancerId = request.Freelancer != Guid.Empty ? request.Freelancer : request.FreelancerId;
        var projectId = request.Project != 0 ? request.Project : request.ProjectId;
        var packageId = request.Package != Guid.Empty ? request.Package : request.PackageId;
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.ClientId == clientId);
        if (project == null) throw new UnauthorizedAccessException("You do not own this project.");

        // Use the resolved values everywhere: looking up request.FreelancerId/request.PackageId
        // would silently query Guid.Empty when the caller used the short field names.
        var freelancer = await _db.FreelancerProfiles.FirstOrDefaultAsync(p => p.Id == freelancerId);
        if (freelancer == null) throw new ArgumentException("Freelancer not found.");
        if (freelancer.UserId == userId) throw new InvalidOperationException("You cannot invite yourself.");

        var existingProposal = await _db.Proposals.AnyAsync(p => p.ProjectId == projectId && p.FreelancerId == freelancerId);
        if (existingProposal) throw new InvalidOperationException("Freelancer has already submitted a proposal for this project.");

        var existingInvitation = await _db.Invitations.AnyAsync(i => i.ProjectId == projectId && i.FreelancerId == freelancerId);
        if (existingInvitation) throw new InvalidOperationException("You have already invited this freelancer to this project.");

        var package = await _db.FreelancerPackages.FirstOrDefaultAsync(p => p.Id == packageId);
        if (package == null) throw new ArgumentException("Invalid package ID.");

        // The package must belong to the freelancer being invited.
        //
        // Every other relationship on this call is checked -- the project is the
        // caller's, the freelancer is not the caller, no duplicate proposal, no
        // duplicate invitation -- and the package was the one left unchecked. It
        // is also the one with money attached: an invitation carries no price of
        // its own, so `BidAmount` is later taken from the package, and
        // `UpdateInvitationStatusAsync` builds the proposal from it.
        //
        // Without this, a client could invite freelancer B while quoting
        // freelancer A's package price, and the auto-created proposal would carry
        // someone else's rate.
        if (package.UserId != freelancer.UserId)
            throw new ArgumentException("That package does not belong to the invited freelancer.");

        var invitation = new Invitation
        {
            ClientId = clientId,
            FreelancerId = freelancerId,
            ProjectId = projectId,
            PackageId = packageId,
            Message = request.Message,
            Status = InvitationStatus.Pending
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                freelancer.UserId,
                NotificationType.InvitationReceived,
                $"You received a new invitation for '{project.Title}'",
                "Invitation",
                invitation.Id);
        }

        return await GetInvitationResponseAsync(invitation.Id);
    }

    public async Task<List<InvitationResponse>> GetSentInvitationsAsync(Guid userId)
    {
        var clientId = await GetClientIdAsync(userId);

        var invitations = await _db.Invitations.Include(i => i.Client).ThenInclude(c => c.User).Include(i => i.Freelancer).ThenInclude(f => f.User).Include(i => i.Project).Where(i => i.ClientId == clientId).OrderByDescending(i => i.CreatedAt).ToListAsync();

        var info = await BuildContractInfoMapAsync(invitations.Select(i => i.ProjectId));
        return invitations.Select(i => MapToResponse(i, info.GetValueOrDefault(i.ProjectId))).ToList();
    }

    public async Task<List<InvitationResponse>> GetReceivedInvitationsAsync(Guid userId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);

        var invitations = await _db.Invitations.Include(i => i.Client).ThenInclude(c => c.User).Include(i => i.Freelancer).ThenInclude(f => f.User).Include(i => i.Project).Where(i => i.FreelancerId == freelancerId).OrderByDescending(i => i.CreatedAt).ToListAsync();

        var info = await BuildContractInfoMapAsync(invitations.Select(i => i.ProjectId));
        return invitations.Select(i => MapToResponse(i, info.GetValueOrDefault(i.ProjectId))).ToList();
    }

    public async Task<PagedResponse<InvitationResponse>> GetPagedSentInvitationsAsync(Guid userId, InvitationStatus? status, int page, int pageSize)
    {
        var clientId = await GetClientIdAsync(userId);
        var query = _db.Invitations.Include(i => i.Client).ThenInclude(c => c.User).Include(i => i.Freelancer).ThenInclude(f => f.User).Include(i => i.Project).Where(i => i.ClientId == clientId).AsQueryable();
        if (status.HasValue) query = query.Where(i => i.Status == status.Value);
        return await PageAsync(query, page, pageSize);
    }

    public async Task<PagedResponse<InvitationResponse>> GetPagedReceivedInvitationsAsync(Guid userId, InvitationStatus? status, int page, int pageSize, bool closedOnly = false)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        var scoped = _db.Invitations.Where(i => i.FreelancerId == freelancerId);

        // Tab counts, computed from the UNFILTERED set: the labels on every tab
        // have to be right regardless of which tab is active, so these cannot
        // come from the paged query below (whose `Count` reflects `status`).
        //
        // One GroupBy rather than four round trips.
        var grouped = await scoped
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        int CountOf(InvitationStatus s) => grouped.FirstOrDefault(g => g.Status == s)?.Count ?? 0;

        var query = _db.Invitations.Include(i => i.Client).ThenInclude(c => c.User).Include(i => i.Freelancer).ThenInclude(f => f.User).Include(i => i.Project).Where(i => i.FreelancerId == freelancerId).AsQueryable();
        if (closedOnly)
        {
            query = query.Where(i => i.Status == InvitationStatus.Declined || i.Status == InvitationStatus.Cancelled);
        }
        else if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }
        var paged = await PageAsync(query, page, pageSize);

        // "closed" is a UI grouping, not a status: declined + cancelled. The enum
        // has no such member, and inventing one would mean storing a value that no
        // longer describes the row.
        paged.TotalCounts = new Dictionary<string, int>
        {
            ["all"] = grouped.Sum(g => g.Count),
            ["pending"] = CountOf(InvitationStatus.Pending),
            ["accepted"] = CountOf(InvitationStatus.Accepted),
            ["closed"] = CountOf(InvitationStatus.Declined) + CountOf(InvitationStatus.Cancelled),
        };
        return paged;
    }

    private async Task<PagedResponse<InvitationResponse>> PageAsync(IQueryable<Invitation> query, int page, int pageSize)
    {
        var p = Math.Max(page, 1); var size = Math.Clamp(pageSize, 1, 100); var count = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync();
        var info = await BuildContractInfoMapAsync(items.Select(i => i.ProjectId));
        var match = await BuildMatchContextMapAsync(items);
        return new PagedResponse<InvitationResponse> { Results = items.Select(i => MapToResponse(i, info.GetValueOrDefault(i.ProjectId), match.GetValueOrDefault(i.Id))).ToList(), Count = count, Page = p, PageSize = size };
    }

    /// <summary>
    /// The AI reasoning behind each invitation, keyed by invitation id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A freelancer invited from an AI-matched shortlist has no way to tell that
    /// from one a client picked by hand, and no way to learn why they were
    /// chosen. That is the one piece of information that would let them judge
    /// whether it is worth their time -- so it is read here rather than left in a
    /// table only the client and the internal API can see.
    /// </para>
    /// <para>
    /// One query for the whole page. A per-invitation lookup would be N+1 on the
    /// endpoint a freelancer opens most often, and the dashboard asks for the
    /// same data again for a preview card.
    /// </para>
    /// <para>
    /// The most recent recommendation per (project, freelancer) wins. An agent
    /// can be re-run against a project, and a freelancer invited both times
    /// should read the current reasoning rather than the first draft of it.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<int, MatchContextDto>> BuildMatchContextMapAsync(IReadOnlyCollection<Invitation> items)
    {
        var map = new Dictionary<int, MatchContextDto>();
        if (items.Count == 0) return map;

        var projectIds = items.Select(i => i.ProjectId).Distinct().ToList();
        var freelancerIds = items.Select(i => i.FreelancerId).Distinct().ToList();

        var rows = await _db.AgentRecommendations
            .AsNoTracking()
            .Where(r => projectIds.Contains(r.ProjectId) && freelancerIds.Contains(r.FreelancerId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        if (rows.Count == 0) return map;

        var newest = new Dictionary<(int, Guid), AgentRecommendation>();
        foreach (var row in rows)
        {
            var key = (row.ProjectId, row.FreelancerId);
            if (!newest.ContainsKey(key)) newest[key] = row;
        }

        foreach (var invitation in items)
        {
            if (!newest.TryGetValue((invitation.ProjectId, invitation.FreelancerId), out var recommendation))
                continue;

            map[invitation.Id] = new MatchContextDto
            {
                Score = recommendation.Score,
                Reason = recommendation.Reason,
                Strengths = WebOfWork.Api.Helpers.RecommendationFactors.ReadStrengths(recommendation.FactorsJson),
                Gaps = WebOfWork.Api.Helpers.RecommendationFactors.ReadGaps(recommendation.FactorsJson),
            };
        }

        return map;
    }

    public async Task<bool> UpdateInvitationStatusAsync(Guid userId, int invitationId, UpdateInvitationStatusRequest request)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);

        var invitation = await _db.Invitations.Include(i => i.Package).Include(i => i.Client).Include(i => i.Freelancer).FirstOrDefaultAsync(i => i.Id == invitationId);

        if (invitation == null) return false;
        if (invitation.FreelancerId != freelancerId) throw new UnauthorizedAccessException("You do not own this invitation.");

        if (invitation.Status != InvitationStatus.Pending) throw new InvalidOperationException("Only pending invitations can be updated.");

        if (request.Status == InvitationStatus.Accepted)
        {
            // Auto-draft a proposal
            var proposal = new Proposal
            {
                ProjectId = invitation.ProjectId,
                FreelancerId = invitation.FreelancerId,
                PackageId = invitation.PackageId,
                CoverLetter = "Proposal automatically created from accepted invitation.",
                BidAmount = invitation.Package.Price,
                DeliveryDays = invitation.Package.DeliveryDays,
                Status = ProposalStatus.Pending
            };
            _db.Proposals.Add(proposal);
        }

        invitation.Status = request.Status;
        await _db.SaveChangesAsync();
        if (_notificationPublisher != null && request.Status == InvitationStatus.Accepted)
        {
            await _notificationPublisher.PublishAsync(
                invitation.Client.UserId,
                NotificationType.ProposalAccepted,
                $"Your invitation was accepted for project #{invitation.ProjectId}.",
                "Invitation",
                invitation.Id);
        }
        return true;
    }

    public async Task<bool> CancelInvitationAsync(Guid userId, int invitationId)
    {
        var clientId = await GetClientIdAsync(userId);

        var invitation = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId);
        if (invitation == null) return false;
        if (invitation.ClientId != clientId) throw new UnauthorizedAccessException("You do not own this invitation.");

        if (invitation.Status != InvitationStatus.Pending) throw new InvalidOperationException("Only pending invitations can be cancelled.");

        invitation.Status = InvitationStatus.Cancelled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<Project>> GetEligibleProjectsAsync(Guid userId)
    {
        var clientId = await GetClientIdAsync(userId);
        return await _db.Projects.Where(p => p.ClientId == clientId && p.Status == ProjectStatus.Open && p.IsActive).OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    private async Task<InvitationResponse> GetInvitationResponseAsync(int invitationId)
    {
        var invitation = await _db.Invitations.Include(i => i.Client).ThenInclude(c => c.User).Include(i => i.Freelancer).ThenInclude(f => f.User).Include(i => i.Project).FirstOrDefaultAsync(i => i.Id == invitationId);

        if (invitation is null) return new InvitationResponse();

        // Same reasoning the paged path attaches, so opening one invitation and
        // listing them cannot disagree about why the freelancer was chosen.
        var match = await BuildMatchContextMapAsync(new[] { invitation });

        return MapToResponse(invitation, null, match.GetValueOrDefault(invitation.Id));
    }

    /// <summary>
    /// Resolves the contract created for each invitation project and precomputes
    /// is_this_freelancer relative to the caller (ProposalsCard/ProposalsPage read it).
    /// </summary>
    private async Task<Dictionary<int, ContractInfo>> BuildContractInfoMapAsync(IEnumerable<int> projectIds)
    {
        var map = new Dictionary<int, ContractInfo>();
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0) return map;

        var currentUserId = _currentUser?.Id;
        Guid? myClientProfile = null, myFreelancerProfile = null;
        if (currentUserId.HasValue)
        {
            myClientProfile = await _db.ClientProfiles.Where(c => c.UserId == currentUserId.Value).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
            myFreelancerProfile = await _db.FreelancerProfiles.Where(f => f.UserId == currentUserId.Value).Select(f => (Guid?)f.Id).FirstOrDefaultAsync();
        }

        var contracts = await _db.Contracts.Where(c => ids.Contains(c.ProjectId)).ToListAsync();
        foreach (var ct in contracts)
        {
            var isThisFreelancer = myFreelancerProfile.HasValue && ct.FreelancerId == myFreelancerProfile.Value;
            var isThisClient = myClientProfile.HasValue && ct.ClientId == myClientProfile.Value;
            map[ct.ProjectId] = new ContractInfo
            {
                Id = ct.Id,
                Status = WebOfWork.Api.Helpers.SnakeCase.Of(ct.Status),
                IsThisFreelancer = isThisFreelancer
            };
        }
        return map;
    }
    // The reference attaches the contract created from an accepted invitation. WebOfWork has no
    // Invitation->Contract navigation, so resolve it through the invitation project.
    private InvitationResponse MapToResponse(Invitation invitation, ContractInfo? contractInfo = null, MatchContextDto? matchContext = null)
    {
        return new InvitationResponse
        {
            Id = invitation.Id,
            // The reference names the FKs client/freelancer/project/package; mirror the _id values into
            // them so the short-name fields are real ids instead of Guid.Empty.
            ClientId = invitation.ClientId,
            Client = invitation.ClientId,
            ClientName = invitation.Client?.User?.FullName ?? string.Empty,
            FreelancerId = invitation.FreelancerId,
            Freelancer = invitation.FreelancerId,
            FreelancerName = invitation.Freelancer?.User?.FullName ?? string.Empty,
            FreelancerImage = invitation.Freelancer?.User?.ProfilePhoto,
            FreelancerTagline = invitation.Freelancer?.Tagline ?? string.Empty,
            ProjectId = invitation.ProjectId,
            Project = invitation.ProjectId,
            ProjectTitle = invitation.Project?.Title ?? string.Empty,
            PackageId = invitation.PackageId,
            Package = invitation.PackageId,
            Message = invitation.Message,
            Status = invitation.Status,
            CreatedAt = invitation.CreatedAt,
            ContractInfo = contractInfo,
            // Null for a hand-picked invitation. Deliberately not defaulted to a
            // placeholder reason: a fabricated explanation is worse than none,
            // because a freelancer would weigh it as though it meant something.
            MatchContext = matchContext
        };
    }

    public sealed class ContractInfo
    {
        public int Id { get; set; }
        /// <summary>snake_case, matching contract.status values the card compares to "active".</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>ProposalsCard/ProposalsPage sort and badge on this.</summary>
        public bool IsThisFreelancer { get; set; }
    }
}
