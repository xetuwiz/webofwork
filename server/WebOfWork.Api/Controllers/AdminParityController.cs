using System;
using WebOfWork.Api.Helpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Authorization;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Common;
using WebOfWork.Api.DTOs.Content;
using WebOfWork.Api.DTOs.Support;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminParityController : ControllerBase
{
    private readonly WebOfWorkDbContext _db;
    private readonly IContentService _content;
    private readonly ISupportService _support;

    public AdminParityController(WebOfWorkDbContext db, IContentService content, ISupportService support)
    {
        _db = db;
        _content = content;
        _support = support;
    }

    // NOTE: GET/PATCH /api/admin/settings/global intentionally live ONLY in
    // PlatformSettingsController. They used to be duplicated here, which made
    // ASP.NET throw "The request matched multiple endpoints" (HTTP 500).
    // PlatformSettingsController is kept because it runs
    // UpdateGlobalSettingsRequestValidator and returns ValidationProblemDetails,
    // which matches the reference DRF serializer error shape.
    // See PlatformSettingsController.GetSettings / UpdateSettings.

    /// <remarks>
    /// Was returning a bare array and ignoring <c>?page=</c> entirely.
    /// <c>PaymenRelease.jsx</c> read <c>paymentData?.next</c> / <c>?.previous</c> to
    /// enable its arrows, and on an array those are always undefined -- so both
    /// buttons were permanently disabled and the page state did nothing. The
    /// client could not have been at fault; there was nothing on the wire to read.
    /// Now that the count, next and previous are actually present, the shared
    /// <c>&lt;Pagination&gt;</c> has something to work with.
    /// <para>
    /// Also accepts <c>page_size</c> as an alias for <c>pageSize</c>, matching the
    /// rest of the API.
    /// </para>
    /// </remarks>
    [HttpGet("pending-payouts")]
    public async Task<IActionResult> PendingPayouts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken ct = default)
    {
        var q = _db.Payments.Include(p => p.Contract).ThenInclude(c => c.Project).Include(p => p.Contract).ThenInclude(c => c.Client).ThenInclude(x => x.User).Include(p => p.Contract).ThenInclude(c => c.Freelancer).ThenInclude(x => x.User).Where(p => p.Status == PaymentStatus.Escrow
                     && p.Contract.Status == ContractStatus.Completed
                     && p.Contract.PayoutEligibleAfter.HasValue
                     && p.Contract.PayoutEligibleAfter.Value <= DateTime.UtcNow);

        // Counted before slicing, or `count` would describe the current page.
        var total = await q.CountAsync(ct);
        var p1 = Math.Max(page, 1);
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);

        var rows = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((p1 - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        var result = new PagedListResponse<object>
        {
            Count = total,
            Page = p1,
            PageSize = size,
            // `.Cast<object>()` rather than `(object)new { ... }`: casting the
            // `new` keyword across a line break does not parse, because C# reads
            // `new` as needing an identifier. Same approach as ProjectsController.
            Results = rows.Select(p => new
        {
            payment_id = $"PAY-{p.Id}",
            contract_id = p.ContractId,
            project_title = p.Contract.Project.Title,
            client_name = p.Contract.Client.User.FullName,
            freelancer_name = p.Contract.Freelancer.User.FullName,
            amount = p.Amount,
            freelancer_share = p.FreelancerShare,
            // `freelancer_share` is the *contracted* share and does not know
            // about refunds, so it overstates what a partially refunded payment
            // will actually pay out. Exposing the refund lets the UI say so
            // rather than quoting a figure the payout will not match --
            // `ReleaseCoreAsync` pays `Amount - RefundedAmount` with the fee
            // pro-rated onto the remainder.
            refunded_amount = p.RefundedAmount,
            platform_fee = p.PlatformFee,
            completed_at = p.Contract.CompletedAt,
            status = WebOfWork.Api.Helpers.SnakeCase.Of(p.Contract.Status),
            dispute_status = (string?)null,
            dispute_raised_by = (string?)null,
            gateway = "PayPal"
        }).Cast<object>().ToList(),
        };

        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    [HttpGet("support/tickets")]
    public async Task<IActionResult> Tickets([FromQuery] string? status, [FromQuery] string? sender_role, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        SupportTicketStatus? s = EnumQueryParser.TryParse<SupportTicketStatus>(status, out var ss) ? ss : null;
        UserRole? r = EnumQueryParser.TryParse<UserRole>(sender_role, out var rr) ? rr : null;
        var x = await _support.GetAdminTicketsAsync(s, r, page, 10, ct);
        return Ok(new
        {
            count = x.TotalCount,
            next = (string?)null,
            previous = (string?)null,
            results = x.Items.Select(t => new
            {
                id = t.Id,
                user = t.UserId,
                user_email = t.UserEmail,
                // Was `t.UserId` -- a Guid, emitted under a key the admin UI renders as a
    // person's name. `GetAdminTicketsAsync` already Includes the User, so the
    // name was loaded and thrown away one layer up.
    user_full_name = t.UserFullName,
                sender_role = WebOfWork.Api.Helpers.SnakeCase.Of(t.SenderRole),
                subject = t.Subject,
                message = t.Message,
                status = WebOfWork.Api.Helpers.SnakeCase.Of(t.Status),
                resolved_at = t.ResolvedAt,
                created_at = t.CreatedAt
            })
        });
    }

    [HttpGet("support/tickets/{id:int}")]
    public async Task<IActionResult> Ticket(int id, CancellationToken ct)
        => Ok(await _support.GetAdminTicketDetailAsync(id, ct));

    [HttpPatch("support/tickets/{id:int}")]
    public async Task<IActionResult> Reply(int id, [FromBody] AdminReplyRequest r, CancellationToken ct)
    {
        try { return Ok(await _support.AdminReplyAsync(id, Guid.Empty, r, ct)); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("cms")]
    public async Task<IActionResult> Cms([FromQuery] CmsSectionCategory? category, CancellationToken ct)
        => Ok(await _content.GetAdminCmsSectionsAsync(ct));

    [HttpPost("cms")]
    public async Task<IActionResult> CreateCms([FromBody] UpsertCmsSectionRequest r, CancellationToken ct)
        => Ok(await _content.CreateCmsSectionAsync(r, ct));

    [HttpPatch("cms")]
    public async Task<IActionResult> PatchCms([FromBody] CmsPatch r, CancellationToken ct)
        => Ok(await _content.UpdateCmsSectionAsync(r.Id, new UpsertCmsSectionRequest
        {
            Category = r.Category,
            Heading = r.Heading,
            Description = r.Description,
            SortOrder = r.SortOrder,
            IsActive = r.IsActive
        }, ct));

    [HttpDelete("cms/{id:int}/delete")]
    public async Task<IActionResult> DeleteCms(int id, CancellationToken ct)
        => await _content.DeleteCmsSectionAsync(id, ct) ? NoContent() : NotFound();

    // FAQ administration used to be duplicated here, under this controller's own
    // "api/admin" prefix, alongside the identical four actions in
    // ContentController ("api/content/admin/faq").
    //
    // The two had drifted apart. This copy's GET returned a bare array while the
    // client read the standard paged envelope, so the admin FAQ table rendered
    // nothing and showed "No FAQs yet" for every admin -- while the public /faq
    // page, hitting a third route entirely, listed the same rows. That looked
    // like contradictory data until you realised they were different URLs.
    //
    // The client now points at ContentController, which already had all four
    // verbs correct, and this duplicate is removed. Mobile only ever calls the
    // public FAQ (AppUrls.faqUrl -> /api/common/public/faq), so nothing else
    // depended on these routes.

    public sealed record CmsPatch(int Id, CmsSectionCategory Category, string Heading, string Description, int SortOrder, bool IsActive);
}
