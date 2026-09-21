using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Milestones;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IMilestoneService
{
    Task<List<MilestoneResponse>> GetMilestonesAsync(Guid userId, int contractId);
    Task<List<MilestoneResponse>> CreateMilestonesAsync(Guid userId, int contractId, List<CreateMilestoneRequest> requests);
    Task<bool> StartMilestoneAsync(Guid userId, int milestoneId);
    Task<bool> SubmitMilestoneAsync(Guid userId, int milestoneId, SubmitMilestoneRequest request);
    Task<bool> ApproveMilestoneAsync(Guid userId, int milestoneId);
    Task<bool> RequestRevisionAsync(Guid userId, int milestoneId);
}

public class MilestoneService : IMilestoneService
{
    private readonly WebOfWorkDbContext _db;

    public MilestoneService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    private async Task<bool> IsParticipantAsync(Guid userId, int contractId)
    {
        var contract = await _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return false;

        return contract.Client.UserId == userId || contract.Freelancer.UserId == userId;
    }

    private async Task<bool> IsClientAsync(Guid userId, int contractId)
    {
        var contract = await _db.Contracts
            .Include(c => c.Client)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        return contract?.Client.UserId == userId;
    }

    private async Task<bool> IsFreelancerAsync(Guid userId, int contractId)
    {
        var contract = await _db.Contracts
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        return contract?.Freelancer.UserId == userId;
    }

    public async Task<List<MilestoneResponse>> GetMilestonesAsync(Guid userId, int contractId)
    {
        if (!await IsParticipantAsync(userId, contractId))
        {
            throw new UnauthorizedAccessException("You are not a participant in this contract.");
        }

        var milestones = await _db.Milestones
            .Include(m => m.Deliverables)
            .Where(m => m.ContractId == contractId)
            .OrderBy(m => m.Order)
            .ToListAsync();

        return milestones.Select(MapToResponse).ToList();
    }

    public async Task<List<MilestoneResponse>> CreateMilestonesAsync(Guid userId, int contractId, List<CreateMilestoneRequest> requests)
    {
        if (!await IsClientAsync(userId, contractId))
        {
            throw new UnauthorizedAccessException("Only the client can create milestones.");
        }

        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) throw new KeyNotFoundException("Contract not found.");

        if (!contract.IsMilestoneBased)
        {
            throw new InvalidOperationException("This contract is not milestone-based.");
        }

        var existingMilestones = await _db.Milestones.AnyAsync(m => m.ContractId == contractId);
        if (existingMilestones)
        {
            throw new InvalidOperationException("Milestones have already been created for this contract.");
        }

        var totalMilestoneAmount = requests.Sum(r => r.Amount);
        if (totalMilestoneAmount != contract.TotalAmount)
        {
            throw new ArgumentException("The sum of milestone amounts must equal the total contract amount.");
        }

        if (requests.Count != contract.MilestoneCount)
        {
            throw new ArgumentException($"Expected {contract.MilestoneCount} milestones, got {requests.Count}.");
        }

        var milestones = new List<Milestone>();
        for (int i = 0; i < requests.Count; i++)
        {
            milestones.Add(new Milestone
            {
                ContractId = contractId,
                Order = i + 1,
                Title = requests[i].Title,
                Amount = requests[i].Amount,
                ExpectedDays = requests[i].ExpectedDays,
                Status = MilestoneStatus.Pending
            });
        }

        _db.Milestones.AddRange(milestones);
        await _db.SaveChangesAsync();

        return milestones.Select(MapToResponse).ToList();
    }

    public async Task<bool> StartMilestoneAsync(Guid userId, int milestoneId)
    {
        var milestone = await _db.Milestones.Include(m => m.Contract).FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone == null) return false;

        if (!await IsClientAsync(userId, milestone.ContractId))
        {
            throw new UnauthorizedAccessException("Only the client can fund/start a milestone.");
        }

        if (milestone.Status != MilestoneStatus.Pending)
        {
            throw new InvalidOperationException("Only pending milestones can be started.");
        }

        milestone.Status = MilestoneStatus.Funded;
        milestone.FundedAt = DateTime.UtcNow;

        // Automatically move to InProgress immediately since funded
        milestone.Status = MilestoneStatus.InProgress;
        milestone.StartedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SubmitMilestoneAsync(Guid userId, int milestoneId, SubmitMilestoneRequest request)
    {
        var milestone = await _db.Milestones.Include(m => m.Contract).FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone == null) return false;

        if (!await IsFreelancerAsync(userId, milestone.ContractId))
        {
            throw new UnauthorizedAccessException("Only the freelancer can submit work for a milestone.");
        }

        if (milestone.Status != MilestoneStatus.InProgress && milestone.Status != MilestoneStatus.RevisionRequested)
        {
            throw new InvalidOperationException("Milestone must be InProgress or RevisionRequested to submit.");
        }

        var deliverable = new MilestoneDeliverable
        {
            MilestoneId = milestoneId,
            FileUrl = request.FileUrl,
            Notes = request.Notes,
            SubmittedAt = DateTime.UtcNow
        };

        _db.MilestoneDeliverables.Add(deliverable);
        
        milestone.Status = MilestoneStatus.Submitted;
        await _db.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> ApproveMilestoneAsync(Guid userId, int milestoneId)
    {
        var milestone = await _db.Milestones.Include(m => m.Contract).FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone == null) return false;

        if (!await IsClientAsync(userId, milestone.ContractId))
        {
            throw new UnauthorizedAccessException("Only the client can approve a milestone.");
        }

        if (milestone.Status != MilestoneStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted milestones can be approved.");
        }

        milestone.Status = MilestoneStatus.Approved;
        milestone.CompletedAt = DateTime.UtcNow;

        // Check if all milestones are complete
        var allMilestones = await _db.Milestones.Where(m => m.ContractId == milestone.ContractId).ToListAsync();
        if (allMilestones.All(m => m.Status == MilestoneStatus.Approved))
        {
            milestone.Contract.Status = ContractStatus.Completed;
            milestone.Contract.CompletedAt = DateTime.UtcNow;
            milestone.Contract.PayoutEligibleAfter = DateTime.UtcNow.AddDays(7);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RequestRevisionAsync(Guid userId, int milestoneId)
    {
        var milestone = await _db.Milestones.Include(m => m.Contract).FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone == null) return false;

        if (!await IsClientAsync(userId, milestone.ContractId))
        {
            throw new UnauthorizedAccessException("Only the client can request a revision.");
        }

        if (milestone.Status != MilestoneStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted milestones can have revisions requested.");
        }

        milestone.Status = MilestoneStatus.RevisionRequested;
        await _db.SaveChangesAsync();
        return true;
    }

    private MilestoneResponse MapToResponse(Milestone milestone)
    {
        return new MilestoneResponse
        {
            Id = milestone.Id,
            ContractId = milestone.ContractId,
            Order = milestone.Order,
            Title = milestone.Title,
            Amount = milestone.Amount,
            ExpectedDays = milestone.ExpectedDays,
            Status = milestone.Status.ToString(),
            StartedAt = milestone.StartedAt,
            CompletedAt = milestone.CompletedAt,
            FundedAt = milestone.FundedAt,
            Deliverables = milestone.Deliverables?.Select(d => new MilestoneDeliverableResponse
            {
                Id = d.Id,
                FileUrl = d.FileUrl,
                Notes = d.Notes,
                SubmittedAt = d.SubmittedAt
            }).ToList() ?? new List<MilestoneDeliverableResponse>()
        };
    }
}
