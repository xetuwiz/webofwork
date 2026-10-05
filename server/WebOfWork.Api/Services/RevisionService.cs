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
    private readonly INotificationPublisher? _notificationPublisher;

    public RevisionService(WebOfWorkDbContext context, INotificationPublisher? notificationPublisher = null)
    {
        _context = context;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<ContractRevisionResponse> RequestRevisionAsync(int contractId, Guid clientUserId, RequestRevisionRequest request)
    {
        var contract = await _context.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            // Project was never loaded, but the notification below reads
            // contract.Project.Title -> NullReferenceException AFTER the revision
            // was already saved. The client got a 500 and no explanation even
            // though the revision had been created, so every retry then failed with
            // "This contract already has a pending revision."
            .Include(c => c.Project)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
            throw new KeyNotFoundException("Contract not found.");

        if (contract.Client.UserId != clientUserId)
            throw new UnauthorizedAccessException("Only the client can request revisions.");

        var hasPendingRevision = await _context.ContractRevisions
            .AnyAsync(r => r.ContractId == contractId && r.Status == RevisionStatus.Pending);
        if (hasPendingRevision)
            throw new InvalidOperationException("This contract already has a pending revision.");

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

        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                contract.Freelancer.UserId,
                NotificationType.RevisionRequested,
                $"Revision requested for '{contract.Project.Title}'",
                "Contract",
                contractId);
        }

        return MapToResponse(revision);
    }

    public async Task<ContractRevisionResponse> ActionRevisionAsync(int revisionId, Guid freelancerUserId, RevisionActionRequest request)
    {
        var revision = await _context.ContractRevisions
            .Include(r => r.Contract).ThenInclude(c => c.Freelancer)
            .Include(r => r.Contract).ThenInclude(c => c.Project)
            .Include(r => r.Contract).ThenInclude(c => c.Client)
            .FirstOrDefaultAsync(r => r.Id == revisionId);

        if (revision == null)
            throw new KeyNotFoundException("Revision not found.");

        if (revision.Contract.Freelancer.UserId != freelancerUserId)
            throw new UnauthorizedAccessException("Only the assigned freelancer can action this revision.");

        if (revision.Status != RevisionStatus.Pending)
            throw new InvalidOperationException("This revision has already been actioned.");

        revision.Status = request.Accept ? RevisionStatus.Accepted : RevisionStatus.Rejected;
        if (request.Accept)
        {
            revision.Contract.Status = ContractStatus.Active;
        }
        if (!request.Accept)
        {
            revision.RejectionMessage = request.RejectionMessage;
        }
        revision.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                revision.Contract.Client.UserId,
                request.Accept ? NotificationType.RevisionAccepted : NotificationType.RevisionRejected,
                request.Accept
                    ? $"Freelancer accepted your revision request for '{revision.Contract.Project.Title}'"
                    : $"Freelancer declined the revision request for '{revision.Contract.Project.Title}'",
                "Contract",
                revision.ContractId);
        }

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

