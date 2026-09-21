using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.DTOs.Contracts;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;
    private readonly ICurrentUser _currentUser;

    public ContractsController(IContractService contractService, ICurrentUser currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetContracts()
    {
        var contracts = await _contractService.GetContractsAsync(_currentUser.Id!.Value);
        return Ok(contracts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetContractDetail(int id)
    {
        try
        {
            var contract = await _contractService.GetContractDetailAsync(_currentUser.Id!.Value, id);
            if (contract == null) return NotFound();
            return Ok(contract);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("offers")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> GetMyOffers()
    {
        var offers = await _contractService.GetMyOffersAsync(_currentUser.Id!.Value);
        return Ok(offers);
    }

    [HttpPost("{id}/accept")]
    [Authorize(Policy = "RequireFreelancer")]
    public async Task<IActionResult> AcceptContract(int id, [FromBody] AcceptContractRequest request)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var success = await _contractService.AcceptContractAsync(_currentUser.Id!.Value, id, request, ip);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateContractStatusRequest request)
    {
        try
        {
            var success = await _contractService.UpdateStatusAsync(_currentUser.Id!.Value, id, request.Status);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("debug-create-offer")]
    [Authorize(Policy = "RequireClient")]
    public async Task<IActionResult> DebugCreateOffer(int projectId, Guid freelancerProfileId, decimal amount, bool isMilestoneBased = false, int milestoneCount = 0)
    {
        // DEBUG ONLY: Creates a contract offer to allow testing Phase 9 before Phase 10 (Payments) is built.
        var db = HttpContext.RequestServices.GetRequiredService<WebOfWork.Api.Data.WebOfWorkDbContext>();

        // Resolve ClientProfile.Id from User.Id (JWT)
        var clientProfile = await db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == _currentUser.Id!.Value);
        if (clientProfile == null) return BadRequest(new { error = "Client profile not found." });

        var contract = new WebOfWork.Api.Models.Contract
        {
            ProjectId = projectId,
            ClientId = clientProfile.Id,
            FreelancerId = freelancerProfileId,
            TotalAmount = amount,
            ClientSignature = "Debug-Client-Signature",
            ClientSignatureType = "type",
            Status = WebOfWork.Api.Models.Enums.ContractStatus.Offered,
            IsMilestoneBased = isMilestoneBased,
            MilestoneCount = milestoneCount
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        return Ok(new { contract.Id, Status = contract.Status.ToString(), contract.TotalAmount, contract.IsMilestoneBased, contract.MilestoneCount });
    }
}
