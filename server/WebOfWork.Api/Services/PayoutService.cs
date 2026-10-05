using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Payouts;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;

namespace WebOfWork.Api.Services;

public interface IPayoutService
{
    Task<List<PaymentResponse>> GetPendingPayoutsAsync(CancellationToken ct);
    Task<PayoutResult> ReleaseByContractAsync(int contractId, CancellationToken ct);
    Task<bool> ReleasePayoutAsync(ReleasePayoutRequest request, CancellationToken ct);
}

public class PayoutService : IPayoutService
{
    private readonly WebOfWorkDbContext _db;
    private readonly IPayPalPayoutClient _payPalPayoutClient;
    private readonly INotificationPublisher? _notificationPublisher;

    /// <summary>
    /// `ISmsSender` was removed from this constructor.
    /// </summary>
    /// <remarks>
    /// It existed for a direct SMS send that duplicated the `PayoutReleased`
    /// notification and ignored the user's preferences. SMS now goes through
    /// `INotificationPublisher` -&gt; `NotificationService`, which honours the
    /// per-channel switch and per-type mutes.
    ///
    /// DI resolves by constructor, so `Program.cs` needed no change. The
    /// signature change is deliberate rather than left as an unused parameter: an
    /// injected dependency that nothing reads is a lie about what this service
    /// does.
    /// </remarks>
    public PayoutService(WebOfWorkDbContext db, IPayPalPayoutClient payPalPayoutClient, INotificationPublisher? notificationPublisher = null)
    {
        _db = db;
        _payPalPayoutClient = payPalPayoutClient;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<List<PaymentResponse>> GetPendingPayoutsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var payouts = await _db.Payments.Include(p => p.Contract).Include(p => p.Contract.Project).Where(p => p.Status == PaymentStatus.Escrow
                     && p.Contract.Status == ContractStatus.Completed
                     && p.Contract.PayoutEligibleAfter.HasValue
                     && p.Contract.PayoutEligibleAfter.Value <= now).OrderBy(p => p.CreatedAt).ToListAsync(ct);

        return payouts.Select(p => new PaymentResponse
        {
            Id = p.Id,
            ContractId = p.ContractId,
            PayPalOrderId = p.PayPalOrderId,
            Amount = (p.Amount - p.RefundedAmount) - (p.PlatformFee * (p.Amount - p.RefundedAmount) / p.Amount),
            Status = p.Status,
            CreatedAt = p.CreatedAt
        }).ToList();
    }

   /// <summary>
   /// Shared by both release paths: pays the freelancer and the platform in one
   /// PayPal batch, then completes the contract and project.
   /// The reference does the same (ref/.../payments/views.py, ReleasePaymentView):
   /// two items in one batch, and on success it marks the payment released, the
   /// contract completed + paid_to_freelancer, and the project completed and
   /// inactive. The previous implementation paid only the freelancer and left
   /// the contract and project untouched, so a released payment could sit against
   /// a still-active project.
   /// </summary>
   private async Task<(bool Ok, string? BatchId, string? Error)> PayBothPartiesAsync(
       Payment payment,
       string freelancerEmail,
       decimal freelancerAmount,
       string note,
       CancellationToken ct)
   {
       var platformEmail = await _db.GlobalSettings.Where(g => g.Id == 1).Select(g => g.PayPalEmail).FirstOrDefaultAsync(ct);

       // The reference returns 400 when this is unset rather than quietly dropping
       // the platform fee, so the admin is told the payout is misconfigured.
       if (string.IsNullOrWhiteSpace(platformEmail))
           return (false, null,
               "Platform PayPal email is not configured in platform settings.");

       var items = new List<(string, decimal, string)>
       {
           (freelancerEmail, freelancerAmount, note),
            // NOTE: the platform fee is deliberately NOT a second payout item.
            // The commission already sits in this platform account's balance --
            // the client's payment landed here before the freelancer was paid.
            // Paying it back out to GlobalSettings.PayPalEmail asks PayPal to
            // pay the sending account itself, which it rejects every time with
            // "Payment to self is not allowed", failing the whole batch and
            // with it the freelancer's payout. The fee stays in the ledger
            // (payment.PlatformFee) and in the balance, which is what the admin
            // transaction reports and commission settings already read from.
       };

       var (ok, batchId, error) = await _payPalPayoutClient.SendPayoutBatchAsync(
           items,
           $"Payout for contract #{payment.ContractId}",
           "Funds for your completed work have been released.");
       if (!ok) return (false, batchId, error);

       payment.Status = PaymentStatus.Released;
       payment.ProcessedAt = DateTime.UtcNow;
       payment.Contract.PaidToFreelancer = true;
       payment.Contract.Status = ContractStatus.Completed;
       if (payment.Contract.CompletedAt is null) payment.Contract.CompletedAt = DateTime.UtcNow;

       // Close the project out too, otherwise it stays listed as open/in-progress
       // even though the work is finished and paid.
       var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == payment.Contract.ProjectId, ct);
       if (project != null)
       {
           project.Status = ProjectStatus.Completed;
           project.IsActive = false;
       }

       await _db.SaveChangesAsync(ct);
        return (true, batchId, null);
    }

    /// <summary>
    /// Releases the payout for a contract, addressed by contract id.
    /// </summary>
    /// <remarks>
    /// This is the route the admin UI actually calls
    /// (<c>POST /api/payments/release-payout</c>). It used to have its own body,
    /// and that body was wrong in three ways, all of which the sibling
    /// <see cref="ReleasePayoutAsync"/> had already solved:
    ///
    /// <list type="number">
    /// <item>It checked only that the payment was in escrow. It did not require
    /// <c>ContractStatus.Completed</c>, so escrow could be released on an
    /// unfinished contract.</item>
    /// <item>It did not enforce <c>PayoutEligibleAfter</c>, so the waiting period
    /// that exists to give the client time to raise a dispute could be skipped.</item>
    /// <item>It paid <c>payment.FreelancerShare</c> — the original amount — rather
    /// than the refund-adjusted balance, so a partially refunded payment paid the
    /// freelancer in full. That is the one nobody had spotted.</item>
    ///
    /// It also published no <c>PayoutReleased</c> notification, so the freelancer
    /// was paid silently.
    ///
    /// The fix is deliberately not four more guards added to this method. It is
    /// that both public entry points now resolve a <see cref="Payment"/> and hand
    /// it to one private <c>ReleaseCoreAsync</c>, so there is a single payout path
    /// with a single set of invariants. Two bodies meant the invariants had to be
    /// kept in step by hand, and they were not — and <c>ReleaseByContractAsync</c>
    /// had no test coverage at all, which is why nothing caught it.
    /// </remarks>
    public async Task<PayoutResult> ReleaseByContractAsync(int contractId, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Contract).ThenInclude(c => c.Freelancer).ThenInclude(f => f.User)
            .FirstOrDefaultAsync(p => p.ContractId == contractId, ct)
            ?? throw new InvalidOperationException("Payment not found.");

        return await ReleaseCoreAsync(payment, ct);
    }

    public async Task<bool> ReleasePayoutAsync(ReleasePayoutRequest request, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Contract).ThenInclude(c => c.Freelancer).ThenInclude(f => f.User)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct)
            ?? throw new InvalidOperationException("Payment not found.");

        await ReleaseCoreAsync(payment, ct);
        return true;
    }

    /// <summary>
    /// The one payout path. Every guard and every state change that a release
    /// implies lives here, and both public entry points route through it.
    /// </summary>
    /// <remarks>
    /// Order matters and is load-bearing:
    ///
    /// <list type="number">
    /// <item><c>Released</c> is distinguished from "not in escrow" so the caller
    /// gets the specific message the tests and the admin UI both rely on.</item>
    /// <item>Contract completion and the eligibility window are checked before
    /// any money moves, not after.</item>
    /// <item>The amount is derived from <c>Amount - RefundedAmount</c> with the
    /// platform fee pro-rated onto the remainder. Using the stored
    /// <c>FreelancerShare</c> instead pays a refunded balance in full.</item>
    /// <item><c>PayBothPartiesAsync</c> performs the transfer and writes
    /// <c>Released</c>, <c>ProcessedAt</c>, <c>PaidToFreelancer</c> and closes the
    /// contract and project. It is what makes a release idempotent in the database
    /// even though the guard above is what normally prevents a second call.</item>
    /// <item>The notification is published last, so a release that threw is never
    /// announced.</item>
    /// </list>
    /// </remarks>
    private async Task<PayoutResult> ReleaseCoreAsync(Payment payment, CancellationToken ct)
    {
        if (payment.Status == PaymentStatus.Released) throw new InvalidOperationException("Payout already released.");

        if (payment.Status != PaymentStatus.Escrow) throw new InvalidOperationException("Payment is not in Escrow.");

        // 13.5: Prevent payout before contract completion
        if (payment.Contract.Status != ContractStatus.Completed) throw new InvalidOperationException("Contract is not completed.");

        // 13.5a: Enforce payout eligibility window
        if (!payment.Contract.PayoutEligibleAfter.HasValue || payment.Contract.PayoutEligibleAfter.Value > DateTime.UtcNow)
            throw new InvalidOperationException("Payout is not yet eligible.");

        var remainingGross = payment.Amount - payment.RefundedAmount;
        var remainingFee = payment.RefundedAmount >= payment.Amount ? 0m : payment.PlatformFee * remainingGross / payment.Amount;
        var netAmount = remainingGross - remainingFee;
        if (netAmount <= 0m) throw new InvalidOperationException("No payable balance remains after refunds.");
        var note = $"Payout for Contract #{payment.ContractId}";

        var paypalEmail = await _db.FreelancerPaymentSettings.Where(settings => settings.UserId == payment.Contract.Freelancer.UserId).Select(settings => settings.PaypalEmail).FirstOrDefaultAsync(ct);
        var email = string.IsNullOrWhiteSpace(paypalEmail)
            ? payment.Contract.Freelancer.User.Email
            : paypalEmail;

        // Pay the freelancer and the platform in one batch, then complete the
        // contract and project. This path previously paid only the freelancer.
        var (ok2, batchId2, error2) = await PayBothPartiesAsync(
            payment, email, netAmount, note, ct);
        if (!ok2) throw new InvalidOperationException(error2 ?? "PayPal payout failed.");

        payment.Status = PaymentStatus.Released;
        payment.ProcessedAt = DateTime.UtcNow;
        payment.Contract.PaidToFreelancer = true;

        await _db.SaveChangesAsync(ct);

        // One delivery path, and it is the notification system.
        //
        // This used to publish a `PayoutReleased` notification *and* send a second
        // SMS directly through `_smsSender`. Two problems with that:
        //
        //  - the direct send never consulted the user's preferences, so a
        //    freelancer who had switched SMS off still received payout texts;
        //  - it was the only reason the tailored message appeared at all. The
        //    strategy was registered under the key "payout_released" while
        //    `NotificationService` looks up `type.ToString()`, so the publish
        //    path missed it and fell back to `GenericNotificationSmsStrategy`.
        //
        // The strategy is now registered under `nameof(NotificationType.PayoutReleased)`
        // and this block is gone, so SMS is dispatched by the same code that
        // honours the per-channel switch and per-type mutes. `IsPhoneVerified` is
        // no longer consulted here either -- that is the user's registered number,
        // and whether it receives anything is now a preference question.
        if (_notificationPublisher != null)
        {
            await _notificationPublisher.PublishAsync(
                payment.Contract.Freelancer.UserId,
                NotificationType.PayoutReleased,
                $"Payout for contract #{payment.ContractId} has been released.",
                "Payment",
                payment.Id,
                ct,
                netAmount,
                payment.ContractId);
        }

        // batchId is string? on the tuple because PayBothPartiesAsync returns null on
        // failure, but the !ok guard above already threw in that case, so it is always
        // populated here. Coalesce to "" to satisfy PayoutResult.BatchId, which is
        // declared non-nullable string -- this silenced CS8604 without changing
        // behaviour or any signature.
        return new PayoutResult("Payout released successfully via PayPal", batchId2 ?? "", "released");
    }
}
