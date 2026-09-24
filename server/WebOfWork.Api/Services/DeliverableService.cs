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
                $"A deliverable was submitted for contract #{contractId}.",
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
}
