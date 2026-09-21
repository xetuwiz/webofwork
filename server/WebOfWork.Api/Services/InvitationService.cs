using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IInvitationService
{
    Task<InvitationResponse> CreateInvitationAsync(Guid userId, CreateInvitationRequest request);
    Task<List<InvitationResponse>> GetSentInvitationsAsync(Guid userId);
    Task<List<InvitationResponse>> GetReceivedInvitationsAsync(Guid userId);
    Task<bool> UpdateInvitationStatusAsync(Guid userId, int invitationId, UpdateInvitationStatusRequest request);
    Task<bool> CancelInvitationAsync(Guid userId, int invitationId);
    Task<List<Project>> GetEligibleProjectsAsync(Guid userId); // For UI dropdowns
}

public class InvitationService : IInvitationService
{
    private readonly WebOfWorkDbContext _db;

    public InvitationService(WebOfWorkDbContext db)
    {
        _db = db;
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

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.ClientId == clientId);
        if (project == null) throw new UnauthorizedAccessException("You do not own this project.");

        var existingProposal = await _db.Proposals.AnyAsync(p => p.ProjectId == request.ProjectId && p.FreelancerId == request.FreelancerId);
        if (existingProposal) throw new InvalidOperationException("Freelancer has already submitted a proposal for this project.");

        var existingInvitation = await _db.Invitations.AnyAsync(i => i.ProjectId == request.ProjectId && i.FreelancerId == request.FreelancerId);
        if (existingInvitation) throw new InvalidOperationException("You have already invited this freelancer to this project.");

        var package = await _db.FreelancerPackages.FirstOrDefaultAsync(p => p.Id == request.PackageId);
        if (package == null) throw new ArgumentException("Invalid package ID.");

        var invitation = new Invitation
        {
            ClientId = clientId,
            FreelancerId = request.FreelancerId,
            ProjectId = request.ProjectId,
            PackageId = request.PackageId,
            Message = request.Message,
            Status = InvitationStatus.Pending
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        return await GetInvitationResponseAsync(invitation.Id);
    }

    public async Task<List<InvitationResponse>> GetSentInvitationsAsync(Guid userId)
    {
        var clientId = await GetClientIdAsync(userId);
        
        var invitations = await _db.Invitations
            .Include(i => i.Client).ThenInclude(c => c.User)
            .Include(i => i.Freelancer).ThenInclude(f => f.User)
            .Include(i => i.Project)
            .Where(i => i.ClientId == clientId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invitations.Select(MapToResponse).ToList();
    }

    public async Task<List<InvitationResponse>> GetReceivedInvitationsAsync(Guid userId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        
        var invitations = await _db.Invitations
            .Include(i => i.Client).ThenInclude(c => c.User)
            .Include(i => i.Freelancer).ThenInclude(f => f.User)
            .Include(i => i.Project)
            .Where(i => i.FreelancerId == freelancerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invitations.Select(MapToResponse).ToList();
    }

    public async Task<bool> UpdateInvitationStatusAsync(Guid userId, int invitationId, UpdateInvitationStatusRequest request)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        
        var invitation = await _db.Invitations
            .Include(i => i.Package)
            .FirstOrDefaultAsync(i => i.Id == invitationId);

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
        return await _db.Projects
            .Where(p => p.ClientId == clientId && p.Status == ProjectStatus.Open && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    private async Task<InvitationResponse> GetInvitationResponseAsync(int invitationId)
    {
        var invitation = await _db.Invitations
            .Include(i => i.Client).ThenInclude(c => c.User)
            .Include(i => i.Freelancer).ThenInclude(f => f.User)
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == invitationId);
            
        return MapToResponse(invitation!);
    }

    private InvitationResponse MapToResponse(Invitation invitation)
    {
        return new InvitationResponse
        {
            Id = invitation.Id,
            ClientId = invitation.ClientId,
            ClientName = invitation.Client?.User?.FullName ?? string.Empty,
            FreelancerId = invitation.FreelancerId,
            FreelancerName = invitation.Freelancer?.User?.FullName ?? string.Empty,
            ProjectId = invitation.ProjectId,
            ProjectTitle = invitation.Project?.Title ?? string.Empty,
            PackageId = invitation.PackageId,
            Message = invitation.Message,
            Status = invitation.Status.ToString(),
            CreatedAt = invitation.CreatedAt
        };
    }
}
