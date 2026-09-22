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
    Task<List<RevenueChartResponse>> GetRevenueChartAsync(int days, CancellationToken ct = default);
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

        return new DashboardStatsResponse
        {
            TotalRevenueUsd = revenueUsd,
            TotalPlatformFeesUsd = platformFeesUsd,
            NetRevenueUsd = revenueUsd - platformFeesUsd,
            TotalPendingPayoutsUsd = pendingPayoutAmount,
            PendingPayoutCount = pendingPayouts.Count,
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalProjects = totalProjects,
            OpenProjects = openProjects,
            CompletedContracts = completedContracts
        };
    }

    public async Task<List<RevenueChartResponse>> GetRevenueChartAsync(int days, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var rows = await _db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Released && p.ProcessedAt >= start)
            .GroupBy(p => p.ProcessedAt!.Value.Date)
            .Select(g => new RevenueChartResponse
            {
                Date = g.Key,
                RevenueUsd = g.Sum(p => p.Amount),
                TotalTransactions = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        // Ensure a day-by-day range is present for the dashboard.
        var result = new List<RevenueChartResponse>();
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            var item = rows.FirstOrDefault(x => x.Date == day);
            result.Add(new RevenueChartResponse
            {
                Date = day,
                RevenueUsd = item?.RevenueUsd ?? 0m,
                TotalTransactions = item?.TotalTransactions ?? 0
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

        var result = new List<ProposalsChartResponse>();
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            var item = rows.FirstOrDefault(x => x.Date == day);
            result.Add(new ProposalsChartResponse
            {
                Date = day,
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
                MilestoneId = p.MilestoneId,
                PayPalOrderId = p.PayPalOrderId,
                Amount = p.Amount - p.PlatformFee,
                Status = p.Status.ToString(),
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return payouts;
    }
}
