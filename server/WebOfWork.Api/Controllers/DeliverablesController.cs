using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebOfWork.Api.Integrations.Cloudinary;
using WebOfWork.Api.DTOs.Deliverables;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeliverablesController : ControllerBase
{
    private readonly IDeliverableService _deliverableService;
    private readonly ICurrentUser _currentUser;
    private readonly ICloudinaryStorageService _cloudinary;

    public DeliverablesController(IDeliverableService deliverableService, ICurrentUser currentUser, ICloudinaryStorageService cloudinary)
    {
        _deliverableService = deliverableService;
        _currentUser = currentUser;
        _cloudinary = cloudinary;
    }

    [HttpPost("contract/{contractId}")]
    [HttpPost("~/api/contracts/{contractId:int}/submit-work")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireFreelancer)]
    // The client posts multipart/form-data (title, notes, plus either a `file` or a
    // Hidden from the generated API description only. Routing and the upload
    // itself are untouched: this is `ApiExplorer`, which Swagger reads from.
    //
    // Swashbuckle 6.6.2 cannot describe `[FromForm] IFormFile` and throws while
    // building the document:
    //
    //   Error reading parameter(s) for action ...SubmitDeliverable
    //   as [FromForm] attribute used with IFormFile
    //
    // That fails the WHOLE document with a 500, so every endpoint becomes
    // invisible rather than just this one. An IOperationFilter cannot fix it,
    // because Swashbuckle reads parameters before filters run -- and
    // Swashbuckle.AspNetCore.Filters ships no file-upload filter (only header and
    // security helpers) despite the error message linking to it.
    //
    // The honest fix is to stop binding IFormFile as a parameter and read
    // `Request.Form.Files` instead, which keeps the endpoint documented. That
    // means changing three working upload paths, so it is deliberately not done
    // as part of restoring the API docs.
    [ApiExplorerSettings(IgnoreApi = true)]
    // `link_url`), but this was declared [FromBody] SubmitDeliverableRequest, so every
    // submission -- file and link alike -- returned 415 Unsupported Media Type.
    // Accept the form, upload any file to Cloudinary, then hand the service the same DTO.
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> SubmitDeliverable(
        int contractId,
        [FromForm] string title,
        [FromForm] string? notes,
        // The client appends "link_url" to the FormData. Form binding is only
        // case-insensitive, not underscore-insensitive, so the default would bind
        // nothing and every link-only submission was rejected as "attach a file
        // or provide a link". Name it exactly as the client sends it.
        [FromForm(Name = "link_url")] string? linkUrl,
        [FromForm] string? deliverableType,
        [FromForm] IFormFile? file)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new { error = "Title is required." });
        }

        string? fileUrl = null;
        if (file is { Length: > 0 })
        {
            if (file.Length > 20 * 1024 * 1024)
            {
                return BadRequest(new { error = "File must be 20 MB or smaller." });
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            fileUrl = await _cloudinary.UploadRawAsync(buffer.ToArray(), file.FileName, "deliverables");
        }

        if (string.IsNullOrWhiteSpace(fileUrl) && string.IsNullOrWhiteSpace(linkUrl))
        {
            return BadRequest(new { error = "Attach a file or provide a link." });
        }

        var request = new SubmitDeliverableRequest(
            title, fileUrl, string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl, notes, deliverableType);

        var result = await _deliverableService.SubmitDeliverableAsync(contractId, userId, request);
        return Ok(result);
    }

    /// <summary>Freelancers had no way to correct or remove a submission.</summary>
    // See SubmitDeliverable for why: Swashbuckle 6.6.2 cannot describe
    // `[FromForm] IFormFile` and fails the entire API description. Hidden from
    // the docs only; routing and the upload are unaffected.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPatch("{deliverableId:int}")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireFreelancer)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UpdateDeliverable(
        int deliverableId,
        [FromForm] string? title,
        [FromForm] string? notes,
        [FromForm(Name = "link_url")] string? linkUrl,
        [FromForm] IFormFile? file)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        string? fileUrl = null;
        if (file is { Length: > 0 })
        {
            if (file.Length > 20 * 1024 * 1024)
            {
                return BadRequest(new { error = "File must be 20 MB or smaller." });
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            fileUrl = await _cloudinary.UploadRawAsync(buffer.ToArray(), file.FileName, "deliverables");
        }

        var request = new UpdateDeliverableRequest(title, fileUrl, linkUrl, notes);
        try
        {
            var result = await _deliverableService.UpdateDeliverableAsync(deliverableId, userId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpDelete("{deliverableId:int}")]
    [Authorize(Policy = WebOfWork.Api.Authorization.AuthorizationPolicies.RequireFreelancer)]
    public async Task<IActionResult> DeleteDeliverable(int deliverableId)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        try
        {
            await _deliverableService.DeleteDeliverableAsync(deliverableId, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpGet("contract/{contractId}")]
    public async Task<IActionResult> GetDeliverables(int contractId)
    {
        if (_currentUser.Id is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await _deliverableService.GetDeliverablesAsync(contractId, userId);
        return Ok(result);
    }
}
