using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Contracts;
using WebOfWork.Api.DTOs.Disputes;
using WebOfWork.Api.DTOs.Reviews;
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
    Task<bool> UpdateStatusAsync(Guid userId, int contractId, ContractStatus status);
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

        var query = _db.Contracts.Include(c => c.Project).Include(c => c.Package).Include(c => c.Client).ThenInclude(p => p.User).Include(c => c.Freelancer).ThenInclude(p => p.User).Where(c => c.ClientId == clientId || c.FreelancerId == freelancerId).AsQueryable();
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

        var contracts = await query.OrderByDescending(c => c.Id).ToListAsync();

        var fees = await LoadPlatformFeesAsync(contracts.Select(c => c.Id));
        // One query for all skills, then map sequentially: the mappers are cheap
        // now that they do no I/O, and this can no longer overlap DbContext use.
        var skills = await LoadProjectSkillsAsync(contracts.Select(c => c.ProjectId));
        var results = new List<ContractResponse>(contracts.Count);
        foreach (var contract in contracts)
            results.Add(MapToResponse(contract, fees.GetValueOrDefault(contract.Id),
                skills.GetValueOrDefault(contract.ProjectId)));
        return results;
    }

    /// <summary>Platform fee per contract, keyed by contract id. Absent = no payment yet.</summary>
    private async Task<Dictionary<int, decimal>> LoadPlatformFeesAsync(IEnumerable<int> contractIds, CancellationToken ct = default)
    {
        var ids = contractIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, decimal>();
        return await _db.Payments.AsNoTracking().Where(p => ids.Contains(p.ContractId)).GroupBy(p => p.ContractId).Select(g => new { ContractId = g.Key, Fee = g.Sum(p => p.PlatformFee) }).ToDictionaryAsync(x => x.ContractId, x => x.Fee, ct);
    }

    public async Task<ContractResponse?> GetContractDetailAsync(Guid userId, int contractId)
    {
        var clientId = await GetClientIdAsync(userId);
        var freelancerId = await GetFreelancerIdAsync(userId);

        // THENIncludes: the Detail page renders the dispute banner and both
        // review cards from this single response, so the rows behind them must
        // be materialised or the navigations come back null.
        var contract = await _db.Contracts.Include(c => c.Project).Include(c => c.Package).Include(c => c.Client).ThenInclude(p => p.User).Include(c => c.Freelancer).ThenInclude(p => p.User).Include(c => c.Deliverables).Include(c => c.Revisions).Include(c => c.Dispute).ThenInclude(d => d!.Evidence).Include(c => c.Payment).Include(c => c.Reviews).ThenInclude(r => r.Reviewer).FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return null;

        if (contract.ClientId != clientId && contract.FreelancerId != freelancerId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this contract.");
        }

        var detailFees = await LoadPlatformFeesAsync(new[] { contract.Id });
        var detailSkills = await LoadProjectSkillsAsync(new[] { contract.ProjectId });
        return MapToResponse(contract, detailFees.GetValueOrDefault(contract.Id), detailSkills.GetValueOrDefault(contract.ProjectId));
    }

    public async Task<List<ContractResponse>> GetMyOffersAsync(Guid userId)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        if (freelancerId == null) return new List<ContractResponse>();

        var offers = await _db.Contracts.Include(c => c.Project).Include(c => c.Package).Include(c => c.Client).ThenInclude(p => p.User).Include(c => c.Freelancer).ThenInclude(p => p.User).Where(c => c.FreelancerId == freelancerId && c.Status == ContractStatus.Offered).OrderByDescending(c => c.Id).ToListAsync();

        var offerFees = await LoadPlatformFeesAsync(offers.Select(c => c.Id));
        var offerSkills = await LoadProjectSkillsAsync(offers.Select(c => c.ProjectId));
        var offerResults = new List<ContractResponse>(offers.Count);
        foreach (var offer in offers)
            offerResults.Add(MapToResponse(offer, offerFees.GetValueOrDefault(offer.Id), offerSkills.GetValueOrDefault(offer.ProjectId)));
        return offerResults;
    }

    public async Task<bool> AcceptContractAsync(Guid userId, int contractId, AcceptContractRequest request, string ipAddress)
    {
        var freelancerId = await GetFreelancerIdAsync(userId);
        var contract = await _db.Contracts.Include(c => c.Project).Include(c => c.Client).ThenInclude(p => p.User).Include(c => c.Freelancer).ThenInclude(p => p.User).FirstOrDefaultAsync(c => c.Id == contractId);

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
                $"Freelancer signed the contract for '{contract.Project.Title}'. Work has started!",
                "Contract",
                contract.Id);
        }
        return true;
    }

    public async Task<bool> UpdateStatusAsync(Guid userId, int contractId, ContractStatus newStatus)
    {
        var clientId = await GetClientIdAsync(userId);
        var freelancerId = await GetFreelancerIdAsync(userId);

        var contract = await _db.Contracts.Include(c => c.Freelancer).FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) return false;

        if (contract.ClientId != clientId && contract.FreelancerId != freelancerId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this contract.");
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
                $"The contract for '{contract.Project.Title}' is officially complete!",
                "Contract",
                contract.Id);
        }
        return true;
    }

    /// <summary>
    /// Skills for many projects in ONE query, keyed by project id.
    /// MapToResponseAsync used to await LoadProjectSkillsAsync per contract while
    /// Task.WhenAll ran those mappers concurrently on a single DbContext, so two
    /// overlapping queries on the same context threw
    /// "A second operation was started on this context instance".
    /// </summary>
    private async Task<Dictionary<int, List<string>>> LoadProjectSkillsAsync(IEnumerable<int> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, List<string>>();
        var links = await _db.ProjectSkills.AsNoTracking().Include(ps => ps.Skill)
            .Where(ps => ids.Contains(ps.ProjectId)).ToListAsync(ct);
        return links.GroupBy(l => l.ProjectId).ToDictionary(
            g => g.Key,
            g => g.Select(l => l.Skill?.Name ?? l.CustomName ?? string.Empty)
                 .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList());
    }

    private async Task<List<string>> LoadProjectSkillsAsync(int projectId)
    {
        var links = await _db.ProjectSkills.AsNoTracking().Include(ps => ps.Skill).Where(ps => ps.ProjectId == projectId).ToListAsync();
        return links.Select(l => l.Skill?.Name ?? l.CustomName ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
    }

    private ContractResponse MapToResponse(Contract contract, decimal platformFee = 0m, List<string>? skills = null)
    {
        return new ContractResponse
        {
            Id = contract.Id,
            ProjectId = contract.ProjectId,
            ProjectTitle = contract.Project?.Title ?? string.Empty,
        ProjectDescription = contract.Project?.Description ?? string.Empty,
        // Requirements / deliverables / technologies live on the PROJECT. They were
        // never mapped here, so ContractDetail always showed the empty-state text.
        Requirements = contract.Project?.Requirements,
        ExpectedDeliverables = contract.Project?.ExpectedDeliverables,
        RequiredTechnologies = skills ?? new List<string>(),
        Skills = skills ?? new List<string>(),
        PackageName = contract.Package?.PackageType ?? string.Empty,
        // The project's expected days is the delivery window; fall back to the package's own.
        DeliveryDays = contract.Package?.DeliveryDays ?? contract.Project?.ExpectedDays ?? 0,
            ClientId = contract.ClientId,
        ClientUserId = contract.Client?.UserId,
            ClientName = contract.Client?.User?.FullName ?? string.Empty,
            FreelancerId = contract.FreelancerId,
        FreelancerUserId = contract.Freelancer?.UserId,
            FreelancerName = contract.Freelancer?.User?.FullName ?? string.Empty,
            PackageId = contract.PackageId,
            TotalAmount = contract.TotalAmount,
        // The reference ContractListSerializer reads escrow_details.freelancer_share
        // here, falling back to 0.00 when the contract has no escrow row.
        FreelancerEarnings = platformFee > 0m
            ? (contract.TotalAmount > platformFee ? contract.TotalAmount - platformFee : 0m)
            : 0m,
            IsFunded = contract.IsFunded,
            PaidToFreelancer = contract.PaidToFreelancer,
            PayoutEligibleAfter = contract.PayoutEligibleAfter,
            ClientHasSigned = !string.IsNullOrEmpty(contract.ClientSignature),
            ClientSignedAt = contract.ClientSignedAt,
            FreelancerHasSigned = !string.IsNullOrEmpty(contract.FreelancerSignature),
            FreelancerSignedAt = contract.FreelancerSignedAt,
            LegalDocumentUrl = contract.LegalDocumentUrl,
            CompletedAt = contract.CompletedAt,
            CreatedAt = contract.CreatedAt,
            Status = contract.Status,
            Deliverables = contract.Deliverables.Select(d => new WebOfWork.Api.DTOs.Deliverables.DeliverableResponse(d.Id, d.ContractId, d.FreelancerId, d.Title, d.FileUrl, d.LinkUrl, d.Notes, d.DeliverableType, d.SubmittedAt)).ToList(),
            Revisions = contract.Revisions.Select(r => new WebOfWork.Api.DTOs.Revisions.ContractRevisionResponse(r.Id, r.ContractId, r.UserId, r.Reason, r.Status, r.RejectionMessage, r.RequestedAt, r.ResolvedAt)).ToList(),
            PaymentStatus = contract.Payment?.Status.ToString(),
            Reviews = MapContractReviews(contract.Reviews),
            DisputeDetails = MapDispute(contract.Dispute)
        };
    }

    /// <summary>
    /// Splits a contract's reviews into the two cards ContractDetail renders:
    /// the client's review of the freelancer (`ReviewType.Freelancer`) and the
    /// freelancer's review of the client (`ReviewType.Client`).
    /// </summary>
    private static ContractReviewsResponse MapContractReviews(ICollection<Review> reviews) => new()
    {
        ClientReview = MapReview(reviews.FirstOrDefault(r => r.Type == ReviewType.Freelancer)),
        FreelancerReview = MapReview(reviews.FirstOrDefault(r => r.Type == ReviewType.Client))
    };

    private static ReviewResponse? MapReview(Review? r)
    {
        if (r == null) return null;
        return new ReviewResponse
        {
            Id = r.Id,
            ContractId = r.ContractId,
            Contract = r.ContractId,
            ReviewerId = r.ReviewerId,
            Reviewer = r.ReviewerId,
            RevieweeId = r.RevieweeId,
            Reviewee = r.RevieweeId,
            Type = r.Type,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt,
            ReviewerName = r.Reviewer?.FullName ?? string.Empty,
            ReviewerPhoto = r.Reviewer?.ProfilePhoto,
            ProjectTitle = r.Contract?.Project?.Title ?? string.Empty
        };
    }

    /// <summary>
    /// Emits the participant-facing dispute view embedded in the contract
    /// response (DisputeService._MapToResponse is the admin/admin detail shape;
    /// this is the same payload minus the payment id lookup, which the contract's
    /// Payment navigation may not be loaded for).
    /// </summary>
    private static DisputeResponse? MapDispute(Dispute? d)
    {
        if (d == null) return null;
        return new DisputeResponse
        {
            Id = d.Id,
            ContractId = d.ContractId,
            PaymentId = d.Contract?.Payment?.Id,
            OpenerId = d.OpenerId,
            // The freelancer ContractDetail compares raised_by_id against the
            // freelancer's user id to badge "Raised by You"; a profile id here
            // would never match.
            RaisedById = d.OpenerId,
            OpenerRole = d.OpenedByClientId.HasValue
                ? "Client"
                : d.OpenedByFreelancerId.HasValue ? "Freelancer" : null,
            Reason = d.Reason,
            Description = d.Description,
            Status = d.Status,
            AdminNotes = d.AdminNotes,
            Resolution = d.Resolution,
            ResolvedAt = d.ResolvedAt,
            CreatedAt = d.CreatedAt,
            EvidenceUrls = d.Evidence?.Select(e => e.FileUrl).ToList() ?? new List<string>()
        };
    }
}
