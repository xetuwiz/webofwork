using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Admin;
using WebOfWork.Api.DTOs.Payments;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IAdminAnalyticsService
{
    Task<DashboardStatsResponse> GetDashboardStatsAsync(CancellationToken ct = default);
    Task<List<RevenueChartResponse>> GetRevenueChartAsync(DateTime from, DateTime? to, CancellationToken ct = default);
    Task<List<ProposalsChartResponse>> GetProposalsChartAsync(int days, CancellationToken ct = default);
    Task<List<PaymentResponse>> GetPendingPayoutsAsync(CancellationToken ct = default);
}

public class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly WebOfWorkDbContext _db;

    public AdminAnalyticsService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardStatsResponse> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var currentMonthPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Released && p.ProcessedAt >= currentMonthStart)
            .ToListAsync(ct);
        var previousMonthPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Released && p.ProcessedAt >= previousMonthStart && p.ProcessedAt < currentMonthStart)
            .ToListAsync(ct);
        var currentMonthRevenue = currentMonthPayments.Sum(p => p.Amount);
        var previousMonthRevenue = previousMonthPayments.Sum(p => p.Amount);

        var currentMonthUsers = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.CreatedAt >= currentMonthStart, ct);
        var previousMonthUsers = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.CreatedAt >= previousMonthStart && u.CreatedAt < currentMonthStart, ct);
        var currentMonthProjects = await _db.Projects.AsNoTracking().CountAsync(p => p.CreatedAt >= currentMonthStart, ct);
        var previousMonthProjects = await _db.Projects.AsNoTracking().CountAsync(p => p.CreatedAt >= previousMonthStart && p.CreatedAt < currentMonthStart, ct);
        var currentMonthCompleted = await _db.Contracts.AsNoTracking().CountAsync(c => c.Status == ContractStatus.Completed && c.CompletedAt >= currentMonthStart, ct);
        var previousMonthCompleted = await _db.Contracts.AsNoTracking().CountAsync(c => c.Status == ContractStatus.Completed && c.CompletedAt >= previousMonthStart && c.CompletedAt < currentMonthStart, ct);

        static decimal PercentChange(decimal current, decimal previous) => previous == 0m ? (current == 0m ? 0m : 100m) : (current - previous) / previous * 100m;

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Released || p.Status == PaymentStatus.Escrow)
            .ToListAsync(ct);

        var revenueUsd = payments.Where(p => p.Status == PaymentStatus.Released).Sum(p => p.Amount);
        var platformFeesUsd = payments.Where(p => p.Status == PaymentStatus.Released).Sum(p => p.PlatformFee);
        var pendingPayouts = await _db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Escrow)
            .ToListAsync(ct);

        var pendingPayoutAmount = pendingPayouts.Sum(p => p.Amount - p.PlatformFee);

        var totalUsers = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted, ct);
        var activeUsers = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Status == UserStatus.Active, ct);
        var totalProjects = await _db.Projects.AsNoTracking().CountAsync(p => p.IsActive, ct);
        var openProjects = await _db.Projects.AsNoTracking().CountAsync(p => p.IsActive && p.Status == ProjectStatus.Open, ct);
        var completedContracts = await _db.Contracts.AsNoTracking().CountAsync(c => c.Status == ContractStatus.Completed, ct);

        // ── Counters and month-over-month deltas read by AdminOverview.jsx ──
        var activeContracts = await _db.Contracts.AsNoTracking()
            .CountAsync(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Submitted, ct);
        var totalProposals = await _db.Proposals.AsNoTracking().CountAsync(ct);
        var totalDisputes = await _db.Disputes.AsNoTracking().CountAsync(ct);

        // Contract has no CreatedAt column; ClientSignedAt is the closest meaningful
        // "contract started" timestamp for the month-over-month delta.
        var previousMonthActiveContracts = await _db.Contracts.AsNoTracking()
            .CountAsync(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Submitted) && c.ClientSignedAt >= previousMonthStart && c.ClientSignedAt < currentMonthStart, ct);
        var currentMonthActiveContracts = await _db.Contracts.AsNoTracking()
            .CountAsync(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Submitted) && c.ClientSignedAt >= currentMonthStart, ct);
        var previousMonthProposals = await _db.Proposals.AsNoTracking().CountAsync(p => p.CreatedAt >= previousMonthStart && p.CreatedAt < currentMonthStart, ct);
        var currentMonthProposals = await _db.Proposals.AsNoTracking().CountAsync(p => p.CreatedAt >= currentMonthStart, ct);
        var previousMonthDisputes = await _db.Disputes.AsNoTracking().CountAsync(d => d.CreatedAt >= previousMonthStart && d.CreatedAt < currentMonthStart, ct);
        var currentMonthDisputes = await _db.Disputes.AsNoTracking().CountAsync(d => d.CreatedAt >= currentMonthStart, ct);

        // Platform revenue is commission earnings, matching "Platform earnings via commissions"
        // on the dashboard card.
        var currentMonthFees = currentMonthPayments.Sum(p => p.PlatformFee);
        var previousMonthFees = previousMonthPayments.Sum(p => p.PlatformFee);

        return new DashboardStatsResponse
        {
            TotalUsers = totalUsers,
            UsersChange = PercentChange(currentMonthUsers, previousMonthUsers),
            TotalProjects = totalProjects,
            ProjectsChange = PercentChange(currentMonthProjects, previousMonthProjects),
            ActiveContracts = activeContracts,
            ContractsChange = PercentChange(currentMonthActiveContracts, previousMonthActiveContracts),
            TotalProposals = totalProposals,
            ProposalsChange = PercentChange(currentMonthProposals, previousMonthProposals),
            TotalDisputes = totalDisputes,
            DisputeChange = PercentChange(currentMonthDisputes, previousMonthDisputes),
            PlatformRevenue = platformFeesUsd,
            RevenueChange = PercentChange(currentMonthFees, previousMonthFees),
            PendingWithdrawals = pendingPayoutAmount,
            PendingWithdrawalsCount = pendingPayouts.Count,

            TotalRevenueUsd = revenueUsd,
            TotalPlatformFeesUsd = platformFeesUsd,
            NetRevenueUsd = revenueUsd - platformFeesUsd,
            TotalPendingPayoutsUsd = pendingPayoutAmount,
            PendingPayoutCount = pendingPayouts.Count,
            ActiveUsers = activeUsers,
            OpenProjects = openProjects,
            CompletedContracts = completedContracts
        };
    }

    public async Task<List<RevenueChartResponse>> GetRevenueChartAsync(DateTime from, DateTime? to, CancellationToken ct = default)
    {
        var start = from.Date;
        var end = (to ?? DateTime.UtcNow).Date.AddDays(1);

        var query = _db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Released && p.ProcessedAt >= start && p.ProcessedAt < end);

        var rows = await query
            .GroupBy(p => p.ProcessedAt!.Value.Date)
            .Select(g => new RevenueChartResponse
            {
                Date = g.Key,
                // Platform earnings are the commission, matching the dashboard's
                // "Platform earnings via commissions" framing.
                Revenue = g.Sum(p => p.PlatformFee),
                RevenueUsd = g.Sum(p => p.Amount),
                TotalTransactions = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        // One row per month, zero-filled so the chart draws a continuous axis.
        var result = new List<RevenueChartResponse>();
        for (var month = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
             month < end;
             month = month.AddMonths(1))
        {
            var inMonth = rows.Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month).ToList();
            result.Add(new RevenueChartResponse
            {
                Date = month,
                Month = month.ToString("MMM yyyy"),
                Revenue = inMonth.Sum(x => x.Revenue),
                RevenueUsd = inMonth.Sum(x => x.RevenueUsd),
                TotalTransactions = inMonth.Sum(x => x.TotalTransactions)
            });
        }

        return result;
    }

    public async Task<List<ProposalsChartResponse>> GetProposalsChartAsync(int days, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var rows = await _db.Proposals
            .AsNoTracking()
            .Where(p => p.CreatedAt >= start)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Submitted = g.Count(),
                Accepted = g.Count(x => x.Status == ProposalStatus.Accepted),
                Rejected = g.Count(x => x.Status == ProposalStatus.Rejected)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        var projectsPerDay = await _db.Projects.AsNoTracking()
            .Where(p => p.CreatedAt >= start)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = new List<ProposalsChartResponse>();
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            var item = rows.FirstOrDefault(x => x.Date == day);
            var proj = projectsPerDay.FirstOrDefault(x => x.Date == day);
            result.Add(new ProposalsChartResponse
            {
                Date = day,
                Projects = proj?.Count ?? 0,
                Proposals = item?.Submitted ?? 0,
                Submitted = item?.Submitted ?? 0,
                Accepted = item?.Accepted ?? 0,
                Rejected = item?.Rejected ?? 0
            });
        }

        return result;
    }

    public async Task<List<PaymentResponse>> GetPendingPayoutsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var payouts = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Contract)
            .Where(p => p.Status == PaymentStatus.Escrow
                && p.Contract.Status == ContractStatus.Completed
                && p.Contract.PayoutEligibleAfter.HasValue
                && p.Contract.PayoutEligibleAfter.Value <= now)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PaymentResponse
            {
                Id = p.Id,
                ContractId = p.ContractId,
                PayPalOrderId = p.PayPalOrderId,
                Amount = p.Amount,
                PlatformFee = p.PlatformFee,
                FreelancerAmount = p.Amount - p.PlatformFee,
                Status = p.Status,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return payouts;
    }
}
