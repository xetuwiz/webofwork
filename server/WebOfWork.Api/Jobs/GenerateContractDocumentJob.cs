using System;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;

namespace WebOfWork.Api.Jobs;

public class GenerateContractDocumentJob
{
    private readonly WebOfWorkDbContext _db;
    private readonly Services.IContractDocumentService _documentService;
    private readonly ILogger<GenerateContractDocumentJob> _logger;

    public GenerateContractDocumentJob(WebOfWorkDbContext db, Services.IContractDocumentService documentService, ILogger<GenerateContractDocumentJob> logger)
    {
        _db = db;
        _documentService = documentService;
        _logger = logger;
    }

    public async Task<byte[]> ExecuteAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _db.Contracts
            .Include(c => c.Project)
            .Include(c => c.Client)
            .ThenInclude(c => c.User)
            .Include(c => c.Freelancer)
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
        {
            throw new KeyNotFoundException($"Contract {contractId} not found.");
        }

        var pdf = await _documentService.GenerateContractPdfAsync(contract);
        _logger.LogInformation("Generated contract PDF for contract {ContractId} ({Bytes} bytes)", contractId, pdf.Length);
        return pdf;
    }
}
