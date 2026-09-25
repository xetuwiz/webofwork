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
    private readonly INotificationPublisher? _notificationPublisher;

    public DisputeService(WebOfWorkDbContext db, ICurrentUser currentUser, IRefundService refundService, INotificationPublisher? notificationPublisher = null)
    {
        _db = db;
        _currentUser = currentUser;
        _refundService = refundService;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<DisputeResponse> CreateDisputeAsync(int contractId, CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id!.Value;

        // The `contractId` parameter is the source of truth. The request also
        // carries `Contract` and `ContractId` (two fields for one value, kept for
        // older clients), and this used to read only those -- ignoring the
        // parameter entirely.
        //
        // The failure that caused: a caller supplying the id in the route, as the
        // route requires, and leaving the body fields at their default 0, got a
        // lookup for contract 0 and a "Contract not found" that pointed at the
        // wrong thing entirely. The id was present in the request -- as the
        // argument.
        var effectiveContractId = contractId != 0
            ? contractId
            : request.Contract != 0 ? request.Contract : request.ContractId;

        if (effectiveContractId == 0)
            throw new ArgumentException(
                "A contract id is required, either as the argument or on the request.",
                nameof(contractId));
        var contract = await _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == effectiveContractId, cancellationToken)
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
            OpenedByClientId = contract.Client.UserId == userId ? contract.ClientId : null,
            OpenedByFreelancerId = contract.Freelancer.UserId == userId ? contract.FreelancerId : null,
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

        if (_notificationPublisher != null)
        {
            var recipientId = contract.Client.UserId == userId ? contract.Freelancer.UserId : contract.Client.UserId;
            await _notificationPublisher.PublishAsync(
                recipientId,
                NotificationType.DisputeOpened,
                $"A dispute was opened for contract #{contractId}.",
                "Dispute",
                dispute.Id,
                cancellationToken);
        }

        return MapToResponse(dispute, request.EvidenceUrls);
    }

    public async Task<List<DisputeResponse>> GetAdminDisputesAsync(CancellationToken cancellationToken)
    {
        var disputes = await _db.Disputes
            .Include(d => d.Evidence)
            .Include(d => d.Contract)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        // DisputeList.jsx renders a client name and a freelancer name on every row,
        // but DisputeResponse carried neither, so both cells rendered blank -- an
        // admin triaging disputes could not tell whose case they were looking at.
        //
        // Resolved in three flat lookups rather than one long join. The chain
        // Dispute -> Contract -> Client/Freelancer -> User has soft-delete filters
        // on two of the levels, and a single multi-join would either drag
        // soft-deleted rows in alongside the live ones or need a left join per
        // level. Query-syntax LINQ also cannot use `?.`, so the null-guarding has
        // to happen in the projection either way.
        var contractIds = disputes.Select(d => d.ContractId).Distinct().ToList();

        var participants = await _db.Contracts
            .AsNoTracking()
            .Where(c => contractIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                ClientUserId = c.Client != null ? c.Client.UserId : (Guid?)null,
                FreelancerUserId = c.Freelancer != null ? c.Freelancer.UserId : (Guid?)null,
            })
            .ToListAsync(cancellationToken);

        var userIds = participants
            .SelectMany(x => new[] { x.ClientUserId, x.FreelancerUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var names = participants.ToDictionary(
            p => p.Id,
            p => (
                ClientName: p.ClientUserId.HasValue && userNames.TryGetValue(p.ClientUserId.Value, out var cn) ? cn : null,
                FreelancerName: p.FreelancerUserId.HasValue && userNames.TryGetValue(p.FreelancerUserId.Value, out var fn) ? fn : null));

        return disputes.Select(d =>
        {
            var response = MapToResponse(d, d.Evidence.Select(e => e.FileUrl).ToList());
            if (names.TryGetValue(d.ContractId, out var pair))
            {
                response.ClientName = pair.ClientName;
                response.FreelancerName = pair.FreelancerName;
            }
            return response;
        }).ToList();
    }

    public async Task<DisputeResponse> GetAdminDisputeDetailAsync(int id, CancellationToken cancellationToken)
    {
        var dispute = await _db.Disputes
            .Include(d => d.Evidence)
            .Include(d => d.Contract)
            .Include(d => d.Contract!.Payment)
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
            .Include(d => d.Contract)
            .FirstOrDefaultAsync(d => d.ContractId == contractId, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        return MapToResponse(dispute, dispute.Evidence.Select(e => e.FileUrl).ToList());
    }

    public async Task<DisputeResponse> ResolveDisputeAsync(int id, ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var dispute = await _db.Disputes
            .Include(d => d.Contract)
            .Include(d => d.Evidence)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        if (dispute.Status != DisputeStatus.Pending && dispute.Status != DisputeStatus.UnderReview)
            throw new InvalidOperationException("Dispute is already resolved or rejected.");

        if (request.Status != DisputeStatus.Resolved && request.Status != DisputeStatus.Rejected)
            throw new InvalidOperationException("A dispute can only be finalized as Resolved or Rejected.");

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
            if (!request.ClientRefundAmountUsd.HasValue || request.ClientRefundAmountUsd.Value <= 0m)
                throw new InvalidOperationException("A split resolution requires ClientRefundAmountUsd.");

            var payment = await _db.Payments.Include(p => p.Refunds)
                .Where(p => p.ContractId == dispute.ContractId && p.Status == PaymentStatus.Escrow)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (payment == null) throw new InvalidOperationException("No escrow payment found to split.");

            await _refundService.RefundPaymentAsync(new RefundPaymentRequest
            {
                PaymentId = payment.Id,
                Amount = request.ClientRefundAmountUsd.Value,
                Reason = string.IsNullOrWhiteSpace(request.AdminNotes) ? "Dispute split refund" : request.AdminNotes
            }, cancellationToken);

            var remaining = payment.Amount - payment.RefundedAmount;
            if (remaining <= 0m) throw new InvalidOperationException("Split refund must leave a positive freelancer balance.");
            dispute.Contract.Status = ContractStatus.Completed;
            dispute.Contract.PayoutEligibleAfter = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (_notificationPublisher != null)
        {
            var participantIds = await _db.Contracts
                .Where(c => c.Id == dispute.ContractId)
                .Select(c => new { c.Client.UserId, FreelancerUserId = c.Freelancer.UserId })
                .FirstAsync(cancellationToken);
            foreach (var recipientId in new[] { participantIds.UserId, participantIds.FreelancerUserId }.Distinct())
            {
                await _notificationPublisher.PublishAsync(
                    recipientId,
                    NotificationType.DisputeResolved,
                    $"The dispute for contract #{dispute.ContractId} was resolved.",
                    "Dispute",
                    dispute.Id,
                    cancellationToken);
            }
        }

        return MapToResponse(dispute, dispute.Evidence.Select(e => e.FileUrl).ToList());
    }

    private static DisputeResponse MapToResponse(Dispute d, List<string> evidenceUrls) => new()
    {
        Id = d.Id,
        ContractId = d.ContractId,
        PaymentId = d.Contract?.Payment?.Id,
            OpenerId = d.OpenerId,
            RaisedById = d.OpenerId,
            OpenerRole = d.OpenedByClientId.HasValue ? "Client" : d.OpenedByFreelancerId.HasValue ? "Freelancer" : null,
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


