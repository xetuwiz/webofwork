using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Disputes;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public class DisputeService : IDisputeService
{
    private readonly WebOfWorkDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IRefundService _refundService;

    public DisputeService(WebOfWorkDbContext db, ICurrentUser currentUser, IRefundService refundService)
    {
        _db = db;
        _currentUser = currentUser;
        _refundService = refundService;
    }

    public async Task<DisputeResponse> CreateDisputeAsync(int contractId, CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id!.Value;

        var contract = await _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken)
            ?? throw new KeyNotFoundException("Contract not found.");

        // Step 11.5: Only a contract participant can open a dispute
        bool isParticipant = contract.Client.UserId == userId || contract.Freelancer.UserId == userId;
        if (!isParticipant)
            throw new UnauthorizedAccessException("Only a contract participant can raise a dispute.");

        // Step 11.5: No duplicate disputes per contract
        var existingDispute = await _db.Disputes
            .AnyAsync(d => d.ContractId == contractId, cancellationToken);
        if (existingDispute)
            throw new ArgumentException("A dispute already exists for this contract.");

        var dispute = new Dispute
        {
            ContractId = contractId,
            OpenerId = userId,
            Reason = request.Reason,
            Description = request.Description,
            Status = DisputeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Disputes.Add(dispute);

        // Add evidence files
        foreach (var url in request.EvidenceUrls)
        {
            _db.DisputeFiles.Add(new DisputeFile
            {
                Dispute = dispute,
                FileUrl = url,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Mark contract as Disputed
        contract.Status = ContractStatus.Disputed;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToResponse(dispute, request.EvidenceUrls);
    }

    public async Task<List<DisputeResponse>> GetAdminDisputesAsync(CancellationToken cancellationToken)
    {
        var disputes = await _db.Disputes
            .Include(d => d.Evidence)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return disputes.Select(d => MapToResponse(d, d.Evidence.Select(e => e.FileUrl).ToList())).ToList();
    }

    public async Task<DisputeResponse> GetAdminDisputeDetailAsync(int id, CancellationToken cancellationToken)
    {
        var dispute = await _db.Disputes
            .Include(d => d.Evidence)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        return MapToResponse(dispute, dispute.Evidence.Select(e => e.FileUrl).ToList());
    }

    public async Task<DisputeResponse> GetParticipantDisputeAsync(int contractId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id!.Value;

        var contract = await _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken)
            ?? throw new KeyNotFoundException("Contract not found.");

        bool isParticipant = contract.Client.UserId == userId || contract.Freelancer.UserId == userId;
        if (!isParticipant)
            throw new UnauthorizedAccessException("Access denied.");

        var dispute = await _db.Disputes
            .Include(d => d.Evidence)
            .FirstOrDefaultAsync(d => d.ContractId == contractId, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        return MapToResponse(dispute, dispute.Evidence.Select(e => e.FileUrl).ToList());
    }

    public async Task<DisputeResponse> ResolveDisputeAsync(int id, ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        var dispute = await _db.Disputes
            .Include(d => d.Contract)
            .Include(d => d.Evidence)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        if (dispute.Status != DisputeStatus.Pending && dispute.Status != DisputeStatus.UnderReview)
            throw new InvalidOperationException("Dispute is already resolved or rejected.");

        dispute.Status = request.Status;
        dispute.AdminNotes = request.AdminNotes;
        dispute.Resolution = request.Resolution;

        if (request.Status == DisputeStatus.Resolved || request.Status == DisputeStatus.Rejected)
            dispute.ResolvedAt = DateTime.UtcNow;

        // Chain the contract status based on resolution
        if (request.Resolution == DisputeResolution.ReleaseToFreelancer)
        {
            dispute.Contract.Status = ContractStatus.Completed;
            dispute.Contract.PaidToFreelancer = true;
        }
        else if (request.Resolution == DisputeResolution.Refund)
        {
            var payment = await _db.Payments
                .Where(p => p.ContractId == dispute.ContractId && p.Status == PaymentStatus.Escrow)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (payment == null)
                throw new InvalidOperationException("No escrow payment found to refund.");

            var reason = string.IsNullOrWhiteSpace(request.AdminNotes)
                ? dispute.Reason.ToString()
                : request.AdminNotes;
            await _refundService.RefundPaymentAsync(new RefundPaymentRequest
            {
                PaymentId = payment.Id,
                Reason = reason
            }, cancellationToken);
        }
        else if (request.Resolution == DisputeResolution.Split)
        {
            dispute.Contract.Status = ContractStatus.Refunded;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return MapToResponse(dispute, dispute.Evidence.Select(e => e.FileUrl).ToList());
    }

    private static DisputeResponse MapToResponse(Dispute d, List<string> evidenceUrls) => new()
    {
        Id = d.Id,
        ContractId = d.ContractId,
        OpenerId = d.OpenerId,
        Reason = d.Reason,
        Description = d.Description,
        Status = d.Status,
        AdminNotes = d.AdminNotes,
        Resolution = d.Resolution,
        ResolvedAt = d.ResolvedAt,
        CreatedAt = d.CreatedAt,
        EvidenceUrls = evidenceUrls
    };
}
