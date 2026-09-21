using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.DTOs.Reviews;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    // Step 11.19: POST /api/reviews — create review (participant)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
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

    // Step 11.26: GET /api/reviews/me/client — get my received client reviews
    [HttpGet("me/client")]
    public async Task<IActionResult> GetMyClientReviews(CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetClientReviewsAsync(cancellationToken);
        return Ok(result);
    }

    // Step 11.27: GET /api/reviews/freelancer/{profileId} — get freelancer reviews by profile id (public)
    [HttpGet("freelancer/{profileId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFreelancerReviews(int profileId, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetFreelancerReviewsByIdAsync(profileId, cancellationToken);
        return Ok(result);
    }

    // Step 11.28a: DELETE /api/reviews/{id} — admin only
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _reviewService.DeleteReviewAsync(id, cancellationToken);
        return NoContent();
    }
}
