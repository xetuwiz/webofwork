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

    [HttpGet("revenue-chart")]
    public async Task<IActionResult> GetRevenueChart([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var chart = await _analytics.GetRevenueChartAsync(days <= 0 ? 30 : days, ct);
        return Ok(chart);
    }

    [HttpGet("proposals-chart")]
    public async Task<IActionResult> GetProposalsChart([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var chart = await _analytics.GetProposalsChartAsync(days <= 0 ? 30 : days, ct);
        return Ok(chart);
    }

    [HttpGet("pending-payouts")]
    public async Task<IActionResult> GetPendingPayouts(CancellationToken ct)
    {
        var payouts = await _analytics.GetPendingPayoutsAsync(ct);
        return Ok(payouts);
    }
}
