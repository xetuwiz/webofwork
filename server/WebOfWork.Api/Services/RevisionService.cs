using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Revisions;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IRevisionService
{
    Task<ContractRevisionResponse> RequestRevisionAsync(int contractId, Guid clientUserId, RequestRevisionRequest request);
    Task<ContractRevisionResponse> ActionRevisionAsync(int revisionId, Guid freelancerUserId, RevisionActionRequest request);
    Task<List<ContractRevisionResponse>> GetRevisionsAsync(int contractId, Guid userId);
}

public class RevisionService : IRevisionService
{
    private readonly WebOfWorkDbContext _context;

    public RevisionService(WebOfWorkDbContext context)
    {
        _context = context;
    }

    public async Task<ContractRevisionResponse> RequestRevisionAsync(int contractId, Guid clientUserId, RequestRevisionRequest request)
    {
        var contract = await _context.Contracts
            .Include(c => c.Client)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
            throw new KeyNotFoundException("Contract not found.");

        if (contract.Client.UserId != clientUserId)
            throw new UnauthorizedAccessException("Only the client can request revisions.");

        var revision = new ContractRevision
        {
            ContractId = contractId,
            UserId = clientUserId,
            Reason = request.Reason,
            Status = RevisionStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        _context.ContractRevisions.Add(revision);
        await _context.SaveChangesAsync();

        return MapToResponse(revision);
    }

    public async Task<ContractRevisionResponse> ActionRevisionAsync(int revisionId, Guid freelancerUserId, RevisionActionRequest request)
    {
        var revision = await _context.ContractRevisions
            .Include(r => r.Contract)
            .ThenInclude(c => c.Freelancer)
            .FirstOrDefaultAsync(r => r.Id == revisionId);

        if (revision == null)
            throw new KeyNotFoundException("Revision not found.");

        if (revision.Contract.Freelancer.UserId != freelancerUserId)
            throw new UnauthorizedAccessException("Only the assigned freelancer can action this revision.");

        if (revision.Status != RevisionStatus.Pending)
            throw new InvalidOperationException("This revision has already been actioned.");

        revision.Status = request.Accept ? RevisionStatus.Accepted : RevisionStatus.Rejected;
        if (!request.Accept)
        {
            revision.RejectionMessage = request.RejectionMessage;
        }
        revision.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(revision);
    }

    public async Task<List<ContractRevisionResponse>> GetRevisionsAsync(int contractId, Guid userId)
    {
        var contract = await _context.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
            throw new KeyNotFoundException("Contract not found.");

        if (contract.Client.UserId != userId && contract.Freelancer.UserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to view these revisions.");

        var revisions = await _context.ContractRevisions
            .Where(r => r.ContractId == contractId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => MapToResponse(r))
            .ToListAsync();

        return revisions;
    }

    private static ContractRevisionResponse MapToResponse(ContractRevision revision)
    {
        return new ContractRevisionResponse(
            revision.Id,
            revision.ContractId,
            revision.UserId,
            revision.Reason,
            revision.Status,
            revision.RejectionMessage,
            revision.RequestedAt,
            revision.ResolvedAt
        );
    }
}
