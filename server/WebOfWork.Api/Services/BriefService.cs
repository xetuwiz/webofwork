using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Briefs;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface IBriefService
{
    Task<BriefResponse> CreateBriefAsync(Guid userId, CreateBriefRequest request);
    Task<List<BriefResponse>> GetMyBriefsAsync(Guid userId);
    Task<BriefResponse?> GetBriefDetailAsync(Guid userId, int briefId);
}

public class BriefService : IBriefService
{
    private readonly WebOfWorkDbContext _db;

    public BriefService(WebOfWorkDbContext db)
    {
        _db = db;
    }

    private async Task<Guid> GetClientIdAsync(Guid userId)
    {
        var profile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
        if (profile == null) throw new UnauthorizedAccessException("Client profile not found.");
        return profile.Id;
    }

    public async Task<BriefResponse> CreateBriefAsync(Guid userId, CreateBriefRequest request)
    {
        var clientId = await GetClientIdAsync(userId);
        var brief = new Brief
        {
            ClientId = clientId,
            Text = request.Text,
            BudgetHint = request.BudgetHint,
            DeadlineHint = request.DeadlineHint,
            Status = BriefStatus.Pending
        };

        _db.Briefs.Add(brief);
        await _db.SaveChangesAsync();

        return MapToResponse(brief);
    }

    public async Task<List<BriefResponse>> GetMyBriefsAsync(Guid userId)
    {
        var clientId = await GetClientIdAsync(userId);
        var briefs = await _db.Briefs
            .Include(b => b.Attachments)
            .Include(b => b.Analysis)
            .Where(b => b.ClientId == clientId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return briefs.Select(MapToResponse).ToList();
    }

    public async Task<BriefResponse?> GetBriefDetailAsync(Guid userId, int briefId)
    {
        var clientId = await GetClientIdAsync(userId);
        var brief = await _db.Briefs
            .Include(b => b.Attachments)
            .Include(b => b.Analysis)
            .FirstOrDefaultAsync(b => b.Id == briefId);

        if (brief == null) return null;
        
        if (brief.ClientId != clientId)
            throw new UnauthorizedAccessException("You do not have permission to access this brief.");

        return MapToResponse(brief);
    }

    private BriefResponse MapToResponse(Brief brief)
    {
        var resp = new BriefResponse
        {
            Id = brief.Id,
            Text = brief.Text,
            BudgetHint = brief.BudgetHint,
            DeadlineHint = brief.DeadlineHint,
            Status = brief.Status,
            CreatedAt = brief.CreatedAt,
            Attachments = brief.Attachments.Select(a => new BriefAttachmentResponse
            {
                Id = a.Id,
                FileUrl = a.FileUrl,
                FileName = a.FileName
            }).ToList()
        };
        
        if (brief.Analysis != null)
        {
            resp.Analysis = new BriefAnalysisResponse
            {
                RequiredSkills = brief.Analysis.RequiredSkills ?? new List<string>(),
                Complexity = brief.Analysis.Complexity,
                SuggestedBudget = brief.Analysis.SuggestedBudget,
                Timeline = brief.Analysis.Timeline,
                Confidence = brief.Analysis.Confidence
            };
        }
        return resp;
    }
}
