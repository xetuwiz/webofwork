using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Reviews;
using WebOfWork.Api.Helpers;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    // CreateReviewValidator enforces rating 1-5 and a non-empty comment. It
    // existed but was never injected, so a rating of 0 (the unset dropdown
    // default) was stored and the freelancer's average was dragged down.
    private readonly Validators.Reviews.CreateReviewValidator _createValidator;

    public ReviewsController(IReviewService reviewService, Validators.Reviews.CreateReviewValidator createValidator)
    {
        _reviewService = reviewService;
        _createValidator = createValidator;
    }

    // Step 11.19: POST /api/reviews — create review (participant)
    [HttpPost]
    [HttpPost("~/api/profile/reviews/create")]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        // The reference alias sends the contract as "contract" (or contract_id); normalise both
        // BEFORE validating, or a legitimate `contract: N` post fails the ContractId > 0 rule.
        if (request.ContractId == 0) request.ContractId = request.Contract;
        var v = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) return BadRequest(new { error = v.Errors.First().ErrorMessage });
        var result = await _reviewService.CreateReviewAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMyFreelancerReviews), result);
    }

    // Step 11.25: GET /api/reviews/me/freelancer — get my received freelancer reviews
    [HttpGet("me/freelancer")]
    public async Task<IActionResult> GetMyFreelancerReviews(CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetFreelancerReviewsAsync(cancellationToken);
        return Ok(result);
    }

    // GET /api/profile/reviews/client/{id}/ — paged, with next/previous.
    [HttpGet("~/api/profile/reviews/client/{id:guid}")]
    public async Task<IActionResult> GetClientReviewsById(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? page_size = null, CancellationToken cancellationToken = default)
    {
        // This API spells the page size both ways; accepting only one means the
        // other is silently dropped and the caller gets 10 rows with no hint why.
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);
        var result = await _reviewService.GetClientReviewsPagedAsync(id, page, size, cancellationToken);
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    // Step 11.26: GET /api/reviews/me/client — get my received client reviews
    [HttpGet("me/client")]
    public async Task<IActionResult> GetMyClientReviews(CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetClientReviewsAsync(cancellationToken);
        return Ok(result);
    }

    // Step 11.27: GET /api/reviews/freelancer/{profileId} — get freelancer reviews by profile id (public)
    [HttpGet("freelancer/{profileId:guid}")]
    [HttpGet("~/api/profile/freelancers/{profileId:guid}/reviews")]
    [AllowAnonymous]
    /// <remarks>
    /// Was returning the whole list as a bare array and ignoring <c>?page=</c>.
    /// <c>AllFreelancerReviews.jsx</c> reads <c>data?.results</c>, <c>data?.count</c>
    /// and <c>data?.next</c>, all of which are undefined on an array -- so the
    /// page rendered no reviews at all and its paginator could never have worked.
    /// Now returns the same envelope as every other list endpoint, with real
    /// paging and links.
    /// </remarks>
    public async Task<IActionResult> GetFreelancerReviews(Guid profileId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? page_size = null, CancellationToken cancellationToken = default)
    {
        var all = await _reviewService.GetFreelancerReviewsByIdAsync(profileId, cancellationToken);
        var p = Math.Max(page, 1);
        var size = Math.Clamp(page_size ?? pageSize, 1, 100);

        var result = new PagedListResponse<ReviewResponse>
        {
            Count = all.Count,
            Page = p,
            PageSize = size,
            Results = all.Skip((p - 1) * size).Take(size).ToList(),
        };
        PaginationLinks.Apply(Request, result.Page, result.PageSize, result.Count, v => result.Next = v, v => result.Previous = v);
        return Ok(result);
    }

    // Step 11.28a: DELETE /api/reviews/{id} — admin only
    [HttpGet("admin")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAdminReviews(CancellationToken cancellationToken)
    {
        return Ok(await _reviewService.GetAllReviewsAsync(cancellationToken));
    }


    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _reviewService.DeleteReviewAsync(id, cancellationToken);
        return NoContent();
    }
}
