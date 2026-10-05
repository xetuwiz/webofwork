using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminAnalyticsService _analytics;

    public AdminDashboardController(IAdminAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("dashboard-stats")]
    public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
    {
        var stats = await _analytics.GetDashboardStatsAsync(ct);
        return Ok(stats);
    }

    // The dashboard sends ?range=this_year|last_year|all_time; the reference buckets the
    // result by month. Map the range to a span, then bucket monthly for the X axis.
    [HttpGet("revenue-chart")]
    public async Task<IActionResult> GetRevenueChart([FromQuery] string? range, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(range);
        var chart = await _analytics.GetRevenueChartAsync(from, to, ct);
        return Ok(chart);
    }

    private static (DateTime From, DateTime? To) ResolveRange(string? range) => range switch
    {
        "last_year" => (new DateTime(DateTime.UtcNow.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        // Monthly zero-fill over an unbounded span would emit thousands of rows;
        // cap the window and let the earliest bucket absorb the remainder.
        "all_time" => (DateTime.UtcNow.AddYears(-5), null),
        _ => (new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), null)
    };

    [HttpGet("proposals-chart")]
    public async Task<IActionResult> GetProposalsChart([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var chart = await _analytics.GetProposalsChartAsync(days <= 0 ? 30 : days, ct);
        return Ok(chart);
    }

    // NOTE: GET /api/admin/pending-payouts is implemented ONLY in
    // AdminParityController, which returns the reference item shape
    // (payment_id "PAY-<id>" taken from Payment.Id, not Contract.Id).
    // It previously also existed here, producing an ambiguous-match 500.
    // AdminAnalyticsService.GetPendingPayoutsAsync remains available for the
    // release workflow via /api/payouts/pending.
}
