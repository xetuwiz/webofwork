using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;

namespace WebOfWork.Api.Services;

public interface IPaymentService
{
    Task<VerifyPaymentResult> VerifyPaymentAndCreateContractAsync(Guid userId, VerifyPaymentRequest request, CancellationToken ct);
    Task<PaymentResponse> VerifyPaymentAsync(Guid userId, VerifyPaymentRequest request, CancellationToken ct);
    Task<decimal> GetClientSpendingSummaryAsync(Guid userId, CancellationToken ct);
    /// <summary>Full spending summary (totals + recent projects) for a client.</summary>
    Task<ClientSpendingSummaryResponse> GetClientSpendingDetailsAsync(Guid userId, CancellationToken ct);
    Task<List<PaymentResponse>> GetFreelancerTransactionHistoryAsync(Guid userId, string? range, CancellationToken ct);

    /// <summary>
    /// The reference-shaped freelancer earnings feed read by EarningsOverview.jsx:
    /// a paged envelope whose <c>results</c> is an object (not an array) holding
    /// <c>total_earning</c>, <c>total_projects</c> and a <c>contracts</c> list.
    /// Rows are per contract, not per payment, and carry contract status.
    /// </summary>
    Task<WebOfWork.Api.DTOs.Payments.FreelancerEarningsResponse> GetFreelancerEarningsAsync(
        Guid userId, string? status, string? range, int page, int pageSize, CancellationToken ct);
    Task<List<PaymentResponse>> GetPlatformTransactionsAsync(CancellationToken ct);
    /// <summary>Admin transaction projection, paged and filterable by status.</summary>
    Task<WebOfWork.Api.DTOs.Payments.PagedAdminTransactionsResponse> GetPagedAdminTransactionsAsync(PaymentStatus? status, int page, int pageSize, CancellationToken ct);
}

public class PaymentService : IPaymentService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalOrderVerifier _payPalVerifier;
    private readonly ICommissionCalculator _commissionCalculator;
    private readonly INotificationPublisher? _notificationPublisher;
    private readonly IPayPalClient? _payPalClient;

    /// <summary>
    /// Starts the validate stage, which is what puts a run into the manager
    /// approval gate.
    /// </summary>
    /// <remarks>
    /// Optional and last in the parameter list, so existing construction sites
    /// are unaffected.
    ///
    /// <b>This deliberately does not block the payment.</b> The plan it came from
    /// says to return 400 when validation fails. Taken literally that makes a
    /// client's completed PayPal payment depend on an LLM service being
    /// reachable, in shape and in latency -- which is a far worse failure than
    /// the one the check guards against. So the run is started, the verdict is
    /// recorded, and an admin sees it in the approval queue. A payment that has
    /// already been taken is not something to refuse on the strength of a model.
    /// </remarks>
    private readonly IAiWorkflowTrigger? _aiTrigger;

    /// <remarks>
    /// `ISmsSender` and `SmsStrategyFactory` used to be constructor parameters.
    /// They were used in exactly one place: a direct `_smsSender.SendSmsAsync`
    /// for a "payment verified" text, issued alongside a published notification.
    /// That was three defects in five lines, all now gone:
    ///
    ///  - the direct send never consulted notification preferences, so a
    ///    freelancer who had switched SMS off still received it;
    ///  - it duplicated the notification, so a freelancer with SMS on got two
    ///    texts about one event;
    ///  - and both said the money had been credited to their account, which at
    ///    this point is false. The payment was just moved to `Escrow`; the funds
    ///    reach the freelancer later, when `PayoutService` releases them and
    ///    publishes `PayoutReleased`.
    ///
    /// Removing the parameters rather than leaving them unused is deliberate: an
    /// injected-but-unread dependency invites the next person to use it, which
    /// is how the direct send came back in the first place.
    /// </remarks>
    public PaymentService(
        WebOfWorkDbContext db,
        IPayPalOrderVerifier payPalVerifier,
        ICommissionCalculator commissionCalculator,
        INotificationPublisher? notificationPublisher = null,
        IPayPalClient? payPalClient = null,
        IAiWorkflowTrigger? aiTrigger = null)
    {
        _db = db;
        _payPalVerifier = payPalVerifier;
        _commissionCalculator = commissionCalculator;
        _notificationPublisher = notificationPublisher;
        _payPalClient = payPalClient;
        _aiTrigger = aiTrigger;
    }

    public async Task<PaymentResponse> VerifyPaymentAsync(Guid userId, VerifyPaymentRequest request, CancellationToken ct)
    {
        var clientProfile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (clientProfile == null) throw new UnauthorizedAccessException("Only clients can verify payments.");

        var contract = await _db.Contracts.Include(c => c.Project).Include(c => c.Freelancer).FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);

        if (contract == null) throw new ArgumentException("Contract not found.");
        if (contract.ClientId != clientProfile.Id) throw new UnauthorizedAccessException("You do not own this project/contract.");

        var existingPayment = await _db.Payments.AnyAsync(p => p.PayPalOrderId == request.PayPalOrderId, ct);
        if (existingPayment) throw new ArgumentException("Payment order ID already processed.");

        var expectedAmountUsd = contract.TotalAmount;

        if (request.PayPalAmountUsd != expectedAmountUsd)
            throw new ArgumentException("PayPal amount does not match the contract amount.");

        var isValid = await _payPalVerifier.VerifyOrderAsync(request.PayPalOrderId, request.PayPalAmountUsd);
        if (!isValid) throw new ArgumentException("Invalid PayPal order. Amount or currency mismatch.");

        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var fee = await _commissionCalculator.CalculateFeeAsync(expectedAmountUsd);
            var orderDetails = _payPalClient == null ? null : await _payPalClient.GetOrderDetailsAsync(request.PayPalOrderId);

            var payment = new Payment
            {
                ContractId = contract.Id,
                PayPalOrderId = request.PayPalOrderId,
                Amount = expectedAmountUsd,
                PlatformFee = fee,
                FreelancerShare = expectedAmountUsd - fee,
                PayPalCaptureId = orderDetails?.CaptureId ?? request.PayPalOrderId,
                Status = PaymentStatus.Escrow,
                ProcessedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);

            contract.IsFunded = true;
            if (contract.Status == ContractStatus.Offered)
            {
                contract.Status = ContractStatus.Active;
            }

            // Reject other proposals if any
            var proposals = await _db.Proposals.Where(p => p.ProjectId == contract.ProjectId && p.Status == ProposalStatus.Pending).ToListAsync(ct);

            foreach (var prop in proposals)
            {
                prop.Status = prop.FreelancerId == contract.FreelancerId ? ProposalStatus.Accepted : ProposalStatus.Rejected;
            }

            // Atomically increment platform analytics.
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO platform_analytics (id, total_volume_usd, total_profit_usd, completed_contracts_count, updated_at)
                VALUES (1, {expectedAmountUsd}, {fee}, 0, {DateTime.UtcNow})
                ON CONFLICT (id) DO UPDATE SET
                    total_volume_usd = platform_analytics.total_volume_usd + EXCLUDED.total_volume_usd,
                    total_profit_usd = platform_analytics.total_profit_usd + EXCLUDED.total_profit_usd,
                    updated_at = EXCLUDED.updated_at
                """, ct);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Validation, after the commit.
            //
            // The ordering is the point. A validate-stage run is what enters the
            // manager approval gate, and before this existed nothing started one,
            // so that gate could never be reached by any flow.
            //
            // It runs *after* the transaction commits and is not allowed to throw,
            // because a client has by this point already paid. If the agent
            // service is down the run simply records nothing and an admin sees an
            // empty validation; refusing the payment would be worse than the thing
            // the validation was meant to catch.
            if (_aiTrigger is not null)
            {
                await _aiTrigger.ValidateBeforeContractAsync(contract.ProjectId, userId, ct);
            }

            // One delivery path, and it is the notification system.
            //
            // There used to be a second one here -- a direct SMS built by the
            // "payment_verified" strategy -- which bypassed preferences and
            // duplicated this notification. It is gone; SMS is now dispatched by
            // the same code that honours the per-channel switch and per-type
            // mutes.
            //
            // The wording is deliberate. The payment was just moved to `Escrow`;
            // the freelancer's share is not theirs until `PayoutService` releases
            // it, which publishes `PayoutReleased` and is the moment the money
            // actually arrives. Telling them here that it has been "credited to
            // your account" was a claim the database contradicted -- and the
            // type name `PaymentCredited` overstates it too, but renaming an
            // enum member changes a value already persisted in `notifications`,
            // which is a migration and a data question rather than a copy fix.
            if (_notificationPublisher != null)
            {
                await _notificationPublisher.PublishAsync(
                    contract.Freelancer.UserId,
                    NotificationType.PaymentCredited,
                    $"The client has paid ${payment.FreelancerShare:N2} into escrow for contract #{contract.Id}. " +
                    "It is released to you when the work is approved.",
                    "Payment",
                    payment.Id,
                    ct,
                    payment.FreelancerShare,
                    payment.ContractId);
            }

            return new PaymentResponse
            {
                Id = payment.Id,
                ContractId = payment.ContractId,
                PayPalOrderId = payment.PayPalOrderId,
                Amount = payment.Amount,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<decimal> GetClientSpendingSummaryAsync(Guid userId, CancellationToken ct)
    {
        var clientProfile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (clientProfile == null) return 0m;

        return await _db.Payments.Include(p => p.Contract).Where(p => p.Contract.ClientId == clientProfile.Id && p.Status != PaymentStatus.Refunded).SumAsync(p => p.Amount, ct);
    }

    public async Task<ClientSpendingSummaryResponse> GetClientSpendingDetailsAsync(Guid userId, CancellationToken ct)
    {
        var clientProfile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (clientProfile == null) return new ClientSpendingSummaryResponse();

        var totalSpent = await _db.Payments.Where(p => p.Contract.ClientId == clientProfile.Id && p.Status != PaymentStatus.Refunded).SumAsync(p => p.Amount, ct);

        var contracts = await _db.Contracts.Where(c => c.ClientId == clientProfile.Id && c.Status != ContractStatus.Cancelled && c.Status != ContractStatus.Refunded).Select(c => new
            {
                c.Id,
                c.TotalAmount,
                c.Status,
                ProjectTitle = c.Project != null ? c.Project.Title : string.Empty,
                FreelancerName = c.Freelancer != null && c.Freelancer.User != null ? c.Freelancer.User.FullName : string.Empty
            }).ToListAsync(ct);

        var completed = contracts.Count(c => c.Status == ContractStatus.Completed);
        var ongoing = contracts.Count(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Submitted || c.Status == ContractStatus.Offered);

        return new ClientSpendingSummaryResponse
        {
            TotalSpent = totalSpent,
            ProjectsCompleted = completed,
            OngoingProjects = ongoing,
            AvgPerProject = contracts.Count == 0 ? 0m : totalSpent / contracts.Count,
            RecentProjects = contracts.OrderByDescending(c => c.Id).Take(10).Select(c => new ClientSpendingProject
                {
                    Id = c.Id,
                    ProjectTitle = c.ProjectTitle,
                    FreelancerName = c.FreelancerName,
                    TotalAmount = c.TotalAmount,
                    Status = c.Status
                }).ToList()
        };
    }

    /// Delegates to the shared <see cref="WebOfWork.Api.Helpers.EnumNaming"/>
    /// so the wire spelling is defined in exactly one place.
    private static string SnakeCase<TEnum>(TEnum status) where TEnum : struct, Enum
        => status.ToSnakeCaseLower();

    public async Task<List<PaymentResponse>> GetFreelancerTransactionHistoryAsync(Guid userId, string? range, CancellationToken ct)
    {
        var freelancerProfile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, ct);
        if (freelancerProfile == null) return new List<PaymentResponse>();

        var from = range?.ToLowerInvariant() switch
        {
            "1m" => DateTime.UtcNow.AddMonths(-1),
            "6m" => DateTime.UtcNow.AddMonths(-6),
            "1y" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.MinValue
        };
        var query = _db.Payments.Include(p => p.Contract).Where(p => p.Contract.FreelancerId == freelancerProfile.Id && p.Status == PaymentStatus.Released);
        if (from != DateTime.MinValue) query = query.Where(p => p.CreatedAt >= from);
        var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

        return payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            PayPalOrderId = p.PayPalOrderId,
            PayPalCaptureId = p.PayPalCaptureId,
            FreelancerShare = p.FreelancerShare == 0m ? p.Amount - p.PlatformFee : p.FreelancerShare,
            Amount = p.Amount - p.PlatformFee,
            Status = p.Status,
            CreatedAt = p.CreatedAt
        }).ToList();
    }


    public async Task<WebOfWork.Api.DTOs.Payments.FreelancerEarningsResponse> GetFreelancerEarningsAsync(
        Guid userId, string? status, string? range, int page, int pageSize, CancellationToken ct)
    {
        var empty = new WebOfWork.Api.DTOs.Payments.FreelancerEarningsResponse
        {
            Count = 0,
            Results = new WebOfWork.Api.DTOs.Payments.FreelancerEarningsSummary()
        };

        var freelancerProfile = await _db.FreelancerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, ct);
        if (freelancerProfile == null) return empty;

        var from = range?.ToLowerInvariant() switch
        {
            "1m" => DateTime.UtcNow.AddMonths(-1),
            "3m" => DateTime.UtcNow.AddMonths(-3),
            "6m" => DateTime.UtcNow.AddMonths(-6),
            "1y" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.MinValue
        };

        var query = _db.Contracts.AsNoTracking().Include(c => c.Project).Include(c => c.Freelancer).ThenInclude(f => f.User).Include(c => c.Payment).Where(c => c.FreelancerId == freelancerProfile.Id);

        // An unrecognised status is ignored rather than rejected: the frontend
        // sends "all" for its unfiltered state and PascalCase badges otherwise.
        if (WebOfWork.Api.Helpers.EnumQueryParser.TryParse<WebOfWork.Api.Models.Enums.ContractStatus>(status, out var parsed))
            query = query.Where(c => c.Status == parsed);
        // No CreatedAt on Contract, so the range filter anchors on ClientSignedAt.
        if (from != DateTime.MinValue) query = query.Where(c => c.ClientSignedAt >= from);

        var count = await query.CountAsync(ct);

        // The reference sums escrow_details__freelancer_share (null -> 0). FreelancerShare
        // is our escrow-share column; fall back to Amount - PlatformFee when unset.
        var earnings = await query.Select(c => new { c.Id, Share = c.Payment == null ? 0m : c.Payment.FreelancerShare }).ToDictionaryAsync(x => x.Id, x => x.Share, ct);

        var p = Math.Max(page, 1);
        var size = Math.Clamp(pageSize, 1, 50);
        var items = await query.OrderByDescending(c => c.ClientSignedAt).ThenByDescending(c => c.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);

        // Totals reflect the whole filtered set, not just the current page.
        var totalEarning = (await query.Select(c => c.Payment == null || c.Payment.FreelancerShare == 0m
                ? (c.Payment == null ? 0m : c.Payment.Amount - c.Payment.PlatformFee)
                : c.Payment.FreelancerShare).ToListAsync(ct)).Sum();
        var totalProjects = count;

        return new WebOfWork.Api.DTOs.Payments.FreelancerEarningsResponse
        {
            Count = count,
            Next = p < Math.Ceiling(count / (double)size) ? $"?page={p + 1}" : null,
            Previous = p > 1 ? $"?page={p - 1}" : null,
            Results = new WebOfWork.Api.DTOs.Payments.FreelancerEarningsSummary
            {
                TotalEarning = totalEarning,
                TotalProjects = totalProjects,
                Contracts = items.Select(c => new WebOfWork.Api.DTOs.Payments.FreelancerEarningContract
                {
                    Id = c.Id,
                    ProjectTitle = c.Project?.Title ?? string.Empty,
                    FreelancerName = c.Freelancer?.User?.FullName ?? string.Empty,
                    Status = WebOfWork.Api.Helpers.SnakeCase.Of(c.Status),
                    TotalAmount = c.TotalAmount,
                    ClientSignedAt = c.ClientSignedAt,
                    FreelancerEarnings = earnings.GetValueOrDefault(c.Id)
                }).ToList()
            }
        };
    }

    public async Task<WebOfWork.Api.DTOs.Payments.PagedAdminTransactionsResponse> GetPagedAdminTransactionsAsync(PaymentStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var p = Math.Max(page, 1);
        var size = Math.Clamp(pageSize, 1, 100);
        var query = _db.Payments.Include(x => x.Contract).ThenInclude(x => x!.Client).ThenInclude(x => x!.User).Include(x => x.Contract).ThenInclude(x => x!.Freelancer).ThenInclude(x => x!.User).AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new WebOfWork.Api.DTOs.Payments.PagedAdminTransactionsResponse
        {
            Count = count,
            Page = p,
            PageSize = size,
            Results = items.Select(x => new WebOfWork.Api.DTOs.Payments.AdminTransactionResponse
            {
                Id = x.Id,
                PaymentId = x.PayPalOrderId,
                ClientName = x.Contract?.Client?.User?.FullName ?? string.Empty,
                FreelancerName = x.Contract?.Freelancer?.User?.FullName ?? string.Empty,
                AmountTotal = x.Amount,
                PlatformFee = x.PlatformFee,
                Status = x.Status,
                Date = x.CreatedAt
            }).ToList()
        };
    }
    public async Task<List<PaymentResponse>> GetPlatformTransactionsAsync(CancellationToken ct)
    {
        var payments = await _db.Payments.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

        return payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            PayPalOrderId = p.PayPalOrderId,
            PayPalCaptureId = p.PayPalCaptureId,
            FreelancerShare = p.FreelancerShare == 0m ? p.Amount - p.PlatformFee : p.FreelancerShare,
            Amount = p.Amount,
            Status = p.Status,
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    public async Task<VerifyPaymentResult> VerifyPaymentAndCreateContractAsync(Guid userId, VerifyPaymentRequest request, CancellationToken ct)
    {
        // Used request.TotalAmount, but the client sends paypal_amount_usd, which binds to
        // PayPalAmountUsd. TotalAmount therefore stayed 0, so the captured order was
        // always compared against 0 and reported as a mismatch. Accept either so the
        // value is never silently zero.
        var expectedUsd = request.PayPalAmountUsd != 0m ? request.PayPalAmountUsd : request.TotalAmount;
        if (!await _payPalVerifier.VerifyOrderAsync(request.PayPalOrderId, expectedUsd)) throw new ArgumentException("Invalid PayPal order. Amount or currency mismatch.");
        var client = await _db.ClientProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct) ?? throw new UnauthorizedAccessException("Client profile not found.");
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == request.ProjectId && x.ClientId == client.Id, ct) ?? throw new KeyNotFoundException("Project not found.");
        var freelancer = await _db.FreelancerProfiles.FirstOrDefaultAsync(x => x.Id == request.FreelancerId, ct) ?? throw new KeyNotFoundException("Freelancer not found.");
        var proposal = await _db.Proposals.FirstOrDefaultAsync(x => x.ProjectId == project.Id && x.FreelancerId == freelancer.Id, ct);
        var invitation = proposal == null ? await _db.Invitations.FirstOrDefaultAsync(x => x.ProjectId == project.Id && x.FreelancerId == freelancer.Id && x.Status == InvitationStatus.Accepted, ct) : null;
        var package = proposal?.Package ?? invitation?.Package;
        var settings = await _db.GlobalSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        var fee = Math.Round(expectedUsd * (settings?.CommissionPercentage ?? 10m) / 100m, 2, MidpointRounding.AwayFromZero);
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        var contract = new Contract { ProjectId = project.Id, ClientId = client.Id, FreelancerId = freelancer.Id, PackageId = package?.Id, TotalAmount = expectedUsd, ClientSignature = request.ClientSignature ?? string.Empty, ClientSignatureType = request.ClientSignatureType ?? "type", ClientIp = null, ClientSignedAt = DateTime.UtcNow, IsFunded = true, Status = ContractStatus.Offered };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync(ct);
        var details = _payPalClient == null ? null : await _payPalClient.GetOrderDetailsAsync(request.PayPalOrderId);
        _db.Payments.Add(new Payment { ContractId = contract.Id, PayPalOrderId = request.PayPalOrderId, PayPalCaptureId = details?.CaptureId, Amount = expectedUsd, PlatformFee = fee, FreelancerShare = expectedUsd - fee, Status = PaymentStatus.Escrow });
        project.Status = ProjectStatus.InProgress;

        // The winning proposal was loaded only to read its package. It was never
        // transitioned to Accepted and the other bids were never rejected, so after
        // escrow cleared the freelancer still saw their own bid as "Pending" in
        // My Proposals. The proposals table also has no contract_id column, so this
        // is the only place the accepted bid can be recorded.
        if (proposal != null)
        {
            proposal.Status = WebOfWork.Api.Models.Enums.ProposalStatus.Accepted;
            var losing = await _db.Proposals
                .Where(x => x.ProjectId == project.Id && x.Id != proposal.Id
                           && x.Status == WebOfWork.Api.Models.Enums.ProposalStatus.Pending)
                .ToListAsync(ct);
            foreach (var other in losing)
                other.Status = WebOfWork.Api.Models.Enums.ProposalStatus.Rejected;
        }

        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new VerifyPaymentResult("Escrow secured via PayPal.", contract.Id);
}
    }
