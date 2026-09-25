using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Deliverables;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IDeliverableService
{
    Task<DeliverableResponse> SubmitDeliverableAsync(int contractId, Guid freelancerUserId, SubmitDeliverableRequest request);
    Task<List<DeliverableResponse>> GetDeliverablesAsync(int contractId, Guid userId);
        Task<DeliverableResponse> UpdateDeliverableAsync(int deliverableId, Guid freelancerUserId, UpdateDeliverableRequest request);
        Task DeleteDeliverableAsync(int deliverableId, Guid freelancerUserId);
}

public class DeliverableService : IDeliverableService
{
    private readonly WebOfWorkDbContext _context;
    private readonly INotificationPublisher? _notificationPublisher;

    public DeliverableService(WebOfWorkDbContext context, INotificationPublisher? notificationPublisher = null)
    {
        _context = context;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<DeliverableResponse> SubmitDeliverableAsync(int contractId, Guid freelancerUserId, SubmitDeliverableRequest request)
    {
        var contract = await _context.Contracts
            .Include(c => c.Project)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
            throw new KeyNotFoundException("Contract not found.");

        if (contract.Freelancer.UserId != freelancerUserId)
            throw new UnauthorizedAccessException("Only the assigned freelancer can submit deliverables for this contract.");

        // Additional checks like contract status could go here

        var deliverable = new ContractDeliverable
        {
            ContractId = contractId,
            FreelancerId = contract.Freelancer.Id,
            Title = request.Title,
            FileUrl = request.FileUrl,
            LinkUrl = request.LinkUrl,
            Notes = request.Notes,
            DeliverableType = string.IsNullOrWhiteSpace(request.DeliverableType) ? (request.FileUrl != null ? "file" : "link") : request.DeliverableType,
            SubmittedAt = DateTime.UtcNow
        };

        // The contract must move to Submitted. Without this the deliverable was
        // stored and the client notified, but the contract stayed Active -- so the
        // "approve / request revision" actions on the client contract screen had
        // nothing to act on and the submitted state was unreachable.
        if (contract.Status == WebOfWork.Api.Models.Enums.ContractStatus.Active)
        {
            contract.Status = WebOfWork.Api.Models.Enums.ContractStatus.Submitted;
        }

        _context.ContractDeliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        if (_notificationPublisher != null)
        {
            var clientUserId = await _context.ClientProfiles
                .Where(p => p.Id == contract.ClientId)
                .Select(p => p.UserId)
                .FirstAsync();
            await _notificationPublisher.PublishAsync(
                clientUserId,
                NotificationType.WorkDelivered,
                $"New work has been delivered for '{contract.Project.Title}'",
                "Contract",
                contractId);
        }

        return new DeliverableResponse(
            deliverable.Id,
            deliverable.ContractId,
            deliverable.FreelancerId,
            deliverable.Title,
            deliverable.FileUrl,
            deliverable.LinkUrl,
            deliverable.Notes,
            deliverable.DeliverableType,
            deliverable.SubmittedAt
        );
    }

    public async Task<List<DeliverableResponse>> GetDeliverablesAsync(int contractId, Guid userId)
    {
        var contract = await _context.Contracts
            .Include(c => c.Client)
            .Include(c => c.Project)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
            throw new KeyNotFoundException("Contract not found.");

        if (contract.Client.UserId != userId && contract.Freelancer.UserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to view these deliverables.");

        var deliverables = await _context.ContractDeliverables
            .Where(d => d.ContractId == contractId)
            .OrderByDescending(d => d.SubmittedAt)
            .Select(d => new DeliverableResponse(
                d.Id,
                d.ContractId,
                d.FreelancerId,
                d.Title,
                d.FileUrl,
                d.LinkUrl,
                d.Notes,
                d.DeliverableType,
                d.SubmittedAt
            ))
            .ToListAsync();

        return deliverables;
    }


        /// <summary>
        /// A submission may only be edited or removed while the work is still in
        /// review. Once the client has approved (Completed) or the contract was
        /// cancelled, disputed or refunded, the record is part of the audit trail --
        /// otherwise a freelancer could delete the evidence of what was delivered
        /// for an already-paid contract.
        /// </summary>
        private static void EnsureMutable(ContractStatus status)
        {
            if (status is ContractStatus.Completed
                or ContractStatus.Cancelled
                or ContractStatus.Disputed
                or ContractStatus.Refunded)
            {
                throw new InvalidOperationException(
                    "This deliverable can no longer be changed because the contract is "
                    + status.ToString().ToLowerInvariant()
                    + ". Contact support if something needs to be corrected.");
            }
        }

        public async Task<DeliverableResponse> UpdateDeliverableAsync(
            int deliverableId, Guid freelancerUserId, UpdateDeliverableRequest request)
        {
            var deliverable = await _context.ContractDeliverables
                .Include(d => d.Contract)
                    .ThenInclude(c => c.Freelancer)
                .FirstOrDefaultAsync(d => d.Id == deliverableId);

            if (deliverable == null)
                throw new KeyNotFoundException("Deliverable not found.");

            if (deliverable.Contract.Freelancer.UserId != freelancerUserId)
                throw new UnauthorizedAccessException("Only the assigned freelancer can edit this deliverable.");

            EnsureMutable(deliverable.Contract.Status);

            if (!string.IsNullOrWhiteSpace(request.Title))
                deliverable.Title = request.Title;
            if (request.Notes != null)
                deliverable.Notes = request.Notes;
            if (request.LinkUrl != null)
                deliverable.LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl;
            if (!string.IsNullOrWhiteSpace(request.FileUrl))
                deliverable.FileUrl = request.FileUrl;

            // Keep the type honest after a swap between an uploaded file and a bare link.
            deliverable.DeliverableType = !string.IsNullOrWhiteSpace(deliverable.FileUrl) ? "file" : "link";
            deliverable.SubmittedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new DeliverableResponse(
                deliverable.Id,
                deliverable.ContractId,
                deliverable.FreelancerId,
                deliverable.Title,
                deliverable.FileUrl,
                deliverable.LinkUrl,
                deliverable.Notes,
                deliverable.DeliverableType,
                deliverable.SubmittedAt
            );
        }

        public async Task DeleteDeliverableAsync(int deliverableId, Guid freelancerUserId)
        {
            var deliverable = await _context.ContractDeliverables
                .Include(d => d.Contract)
                    .ThenInclude(c => c.Freelancer)
                .FirstOrDefaultAsync(d => d.Id == deliverableId);

            if (deliverable == null)
                throw new KeyNotFoundException("Deliverable not found.");

            if (deliverable.Contract.Freelancer.UserId != freelancerUserId)
                throw new UnauthorizedAccessException("Only the assigned freelancer can delete this deliverable.");

            EnsureMutable(deliverable.Contract.Status);

            _context.ContractDeliverables.Remove(deliverable);
            await _context.SaveChangesAsync();
        }
}
