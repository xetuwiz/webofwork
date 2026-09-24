using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Contracts;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.Cloudinary;

namespace WebOfWork.Api.Services;

public interface IContractService
{
    Task<List<ContractResponse>> GetContractsAsync(Guid userId, string? search = null, ContractStatus? status = null, string? role = null);
    Task<ContractResponse?> GetContractDetailAsync(Guid userId, int contractId);
    Task<List<ContractResponse>> GetMyOffersAsync(Guid userId);
    Task<bool> AcceptContractAsync(Guid userId, int contractId, AcceptContractRequest request, string ipAddress);
    Task<bool> UpdateStatusAsync(Guid userId, int contractId, string status);
}

public class ContractService : IContractService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IContractDocumentService _documentService;
    private readonly ICloudinaryStorageService _cloudinaryService;
    private readonly INotificationPublisher? _notificationPublisher;

    public ContractService(WebOfWorkDbContext db, IContractDocumentService documentService, ICloudinaryStorageService cloudinaryService, INotificationPublisher? notificationPublisher = null)
    {
        _db = db;
        _documentService = documentService;
        _cloudinaryService = cloudinaryService;
        _notificationPublisher = notificationPublisher;
    }

    private async Task<Guid?> GetFreelancerIdAsync(Guid userId)
    {
        var profile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId);
        return profile?.Id;
    }

    private async Task<Guid?> GetClientIdAsync(Guid userId)
    {
        var profile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
        return profile?.Id;
    }

    public async Task<List<ContractResponse>> GetContractsAsync(Guid userId, string? search = null, ContractStatus? status = null, string? role = null)
    {
        var clientId = await GetClientIdAsync(userId);
        var freelancerId = await GetFreelancerIdAsync(userId);

        var query = _db.Contracts
            .Include(c => c.Project)
            .Include(c => c.Client).ThenInclude(p => p.User)
            .Include(c => c.Freelancer).ThenInclude(p => p.User)
            .Where(c => c.ClientId == clientId || c.FreelancerId == freelancerId)
            .AsQueryable();
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (string.Equals(role, "client", StringComparison.OrdinalIgnoreCase)) query = query.Where(c => c.ClientId == clientId);
        else if (string.Equals(role, "freelancer", StringComparison.OrdinalIgnoreCase)) query = query.Where(c => c.FreelancerId == freelancerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase) && int.TryParse(term[4..], out var contractId))
                query = query.Where(c => c.Id == contractId);
            else
                query = query.Where(c => c.Project.Title.Contains(term) || c.Client.User.FullName.Contains(term) || c.Freelancer.User.FullName.Contains(term));
        }

        var contracts = await query
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        return contracts.Select(MapToResponse).ToList();
    }

    public async Task<ContractResponse?> GetContractDetailAsync(Guid userId, int contractId)
    {
        var clientId = await GetClientIdAsync(userId);
        var freelancerId = await GetFreelancerIdAsync(userId);

        var contract = await _db.Contracts
            .Include(c => c.Project)
            .Include(c => c.Package)
            .Include(c => c.Client).ThenInclude(p => p.User)
            .Include(c => c.Freelancer).ThenInclude(p => p.User)
            .Include(c => c.Deliverables)
            .Include(c => c.Revisions)
            .Include(c => c.Dispute)
            .Include(c => c.Payment)
            .Include(c => c.Reviews)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return null;

        if (contract.ClientId != clientId && contract.FreelancerId != freelancerId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this contract.");
        }

        return MapToResponse(contract);
    }

    public async Task<List<ContractResponse>> GetMyOffersAsync(Guid userId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        if (freelancerId == null) return new List<ContractResponse>();

        var offers = await _db.Contracts
            .Include(c => c.Project)
            .Include(c => c.Client).ThenInclude(p => p.User)
            .Include(c => c.Freelancer).ThenInclude(p => p.User)
            .Where(c => c.FreelancerId == freelancerId && c.Status == ContractStatus.Offered)
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        return offers.Select(MapToResponse).ToList();
    }

    public async Task<bool> AcceptContractAsync(Guid userId, int contractId, AcceptContractRequest request, string ipAddress)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        var contract = await _db.Contracts
            .Include(c => c.Project)
            .Include(c => c.Client).ThenInclude(p => p.User)
            .Include(c => c.Freelancer).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return false;

        if (contract.FreelancerId != freelancerId)
        {
            throw new UnauthorizedAccessException("Only the assigned freelancer can accept this contract.");
        }

        if (contract.Status != ContractStatus.Offered)
        {
            throw new InvalidOperationException("Contract is not in an offered state.");
        }

        contract.FreelancerSignature = request.Signature;
        contract.FreelancerSignatureType = request.SignatureType;
        contract.FreelancerSignedAt = DateTime.UtcNow;
        contract.FreelancerIp = ipAddress;
        contract.Status = ContractStatus.Active;
        contract.Project.Status = ProjectStatus.InProgress;

        // Generate PDF Document
        var pdfBytes = await _documentService.GenerateContractPdfAsync(contract);
        var filename = $"contract_{contract.Id}_{DateTime.UtcNow.Ticks}.pdf";
        var pdfUrl = await _cloudinaryService.UploadPdfAsync(pdfBytes, filename);
        contract.LegalDocumentUrl = pdfUrl;

        await _db.SaveChangesAsync();
        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                contract.Client.UserId,
                NotificationType.ContractStarted,
                $"Contract #{contract.Id} has started.",
                "Contract",
                contract.Id);
        }
        return true;
    }

    public async Task<bool> UpdateStatusAsync(Guid userId, int contractId, string statusStr)
    {
        var clientId = await GetClientIdAsync(userId);
        var freelancerId = await GetFreelancerIdAsync(userId);

        var contract = await _db.Contracts
            .Include(c => c.Freelancer)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) return false;

        if (contract.ClientId != clientId && contract.FreelancerId != freelancerId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this contract.");
        }

        if (!Enum.TryParse<ContractStatus>(statusStr, true, out var newStatus))
        {
            throw new ArgumentException("Invalid status.");
        }

        if (!ContractStateMachine.IsValidTransition(contract.Status, newStatus))
        {
            throw new InvalidOperationException($"Invalid state transition from {contract.Status} to {newStatus}.");
        }

        if (newStatus == ContractStatus.Completed && contract.ClientId != clientId)
        {
            throw new UnauthorizedAccessException("Only the client can complete this contract.");
        }

        contract.Status = newStatus;

        if (newStatus == ContractStatus.Completed)
        {
            contract.CompletedAt = DateTime.UtcNow;
            contract.PayoutEligibleAfter = contract.CompletedAt.Value.AddDays(7);
            var project = await _db.Projects.FirstAsync(p => p.Id == contract.ProjectId);
            project.Status = ProjectStatus.Completed;
        }

        await _db.SaveChangesAsync();
        if (newStatus == ContractStatus.Completed && _notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                contract.Freelancer.UserId,
                NotificationType.ContractCompleted,
                $"Contract #{contract.Id} has been completed.",
                "Contract",
                contract.Id);
        }
        return true;
    }

    private ContractResponse MapToResponse(Contract contract)
    {
        return new ContractResponse
        {
            Id = contract.Id,
            ProjectId = contract.ProjectId,
            ProjectTitle = contract.Project?.Title ?? string.Empty,
            ClientId = contract.ClientId,
            ClientName = contract.Client?.User?.FullName ?? string.Empty,
            FreelancerId = contract.FreelancerId,
            FreelancerName = contract.Freelancer?.User?.FullName ?? string.Empty,
            PackageId = contract.PackageId,
            TotalAmount = contract.TotalAmount,
            IsFunded = contract.IsFunded,
            PaidToFreelancer = contract.PaidToFreelancer,
            PayoutEligibleAfter = contract.PayoutEligibleAfter,
            ClientHasSigned = !string.IsNullOrEmpty(contract.ClientSignature),
            ClientSignedAt = contract.ClientSignedAt,
            FreelancerHasSigned = !string.IsNullOrEmpty(contract.FreelancerSignature),
            FreelancerSignedAt = contract.FreelancerSignedAt,
            LegalDocumentUrl = contract.LegalDocumentUrl,
            CompletedAt = contract.CompletedAt,
            Status = contract.Status.ToString(),
            Deliverables = contract.Deliverables.Select(d => new WebOfWork.Api.DTOs.Deliverables.DeliverableResponse(d.Id, d.ContractId, d.FreelancerId, d.Title, d.FileUrl, d.LinkUrl, d.Notes, d.DeliverableType, d.SubmittedAt)).ToList(),
            Revisions = contract.Revisions.Select(r => new WebOfWork.Api.DTOs.Revisions.ContractRevisionResponse(r.Id, r.ContractId, r.UserId, r.Reason, r.Status, r.RejectionMessage, r.RequestedAt, r.ResolvedAt)).ToList(),
            PaymentStatus = contract.Payment?.Status.ToString(),
            Reviews = contract.Reviews.Select(r => (object)new { Id = r.Id, ContractId = r.ContractId, ReviewerId = r.ReviewerId, RevieweeId = r.RevieweeId, Rating = r.Rating, Comment = r.Comment, CreatedAt = r.CreatedAt }).ToList()
        };
    }
}
