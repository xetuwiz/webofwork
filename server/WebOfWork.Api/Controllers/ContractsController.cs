using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetContracts([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? role)
    {
        WebOfWork.Api.Models.Enums.ContractStatus? statusFilter = Enum.TryParse<WebOfWork.Api.Models.Enums.ContractStatus>(status, true, out var parsed) ? parsed : null;
        var contracts = await _contractService.GetContractsAsync(_currentUser.Id!.Value, search, statusFilter, role);
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

}
