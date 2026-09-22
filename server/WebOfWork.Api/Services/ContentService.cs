using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Content;
using WebOfWork.Api.DTOs.Platform;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IContentService
{
    Task<GlobalSettings> GetGlobalSettingsAsync(CancellationToken cancellationToken = default);
    Task<GlobalSettings> UpdateGlobalSettingsAsync(UpdateGlobalSettingsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FaqResponse>> GetPublicFaqsAsync(FaqCategory? category = null, CancellationToken ct = default);
    Task<IReadOnlyList<CmsSectionResponse>> GetPublicCmsByCategoryAsync(CmsSectionCategory category, CancellationToken ct = default);

    Task<FaqResponse> CreateFaqAsync(UpsertFaqRequest request, CancellationToken ct = default);
    Task<FaqResponse> UpdateFaqAsync(int id, UpsertFaqRequest request, CancellationToken ct = default);
    Task<bool> DeleteFaqAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<FaqResponse>> GetAdminFaqsAsync(CancellationToken ct = default);

    Task<CmsSectionResponse> CreateCmsSectionAsync(UpsertCmsSectionRequest request, CancellationToken ct = default);
    Task<CmsSectionResponse> UpdateCmsSectionAsync(int id, UpsertCmsSectionRequest request, CancellationToken ct = default);
    Task<bool> DeleteCmsSectionAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CmsSectionResponse>> GetAdminCmsSectionsAsync(CancellationToken ct = default);
}

public class ContentService : IContentService
{
    private readonly WebOfWorkDbContext _dbContext;

    public ContentService(WebOfWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GlobalSettings> GetGlobalSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Set<GlobalSettings>().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (settings == null)
        {
            settings = new GlobalSettings { Id = 1 };
            _dbContext.Add(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    public async Task<GlobalSettings> UpdateGlobalSettingsAsync(UpdateGlobalSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DefaultCurrency != null && !string.Equals(request.DefaultCurrency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("DefaultCurrency cannot be changed from USD.", nameof(request.DefaultCurrency));
        }

        var settings = await GetGlobalSettingsAsync(cancellationToken);

        if (request.SupportEmail != null) settings.SupportEmail = request.SupportEmail;
        if (request.CommissionPercentage.HasValue) settings.CommissionPercentage = request.CommissionPercentage.Value;
        if (request.PayPalEmail != null) settings.PayPalEmail = request.PayPalEmail;
        if (request.MinBriefConfidence.HasValue) settings.MinBriefConfidence = request.MinBriefConfidence.Value;
        if (request.ProposalScreeningThreshold.HasValue) settings.ProposalScreeningThreshold = request.ProposalScreeningThreshold.Value;
        if (request.MatchScoreThreshold.HasValue) settings.MatchScoreThreshold = request.MatchScoreThreshold.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return settings;
    }

    public async Task<IReadOnlyList<FaqResponse>> GetPublicFaqsAsync(FaqCategory? category, CancellationToken ct)
    {
        var query = _dbContext.Faqs.Where(f => f.IsActive);
        if (category.HasValue) query = query.Where(f => f.Category == category.Value);

        return await query
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.CreatedAt)
            .Select(f => new FaqResponse
            {
                Id = f.Id,
                Question = f.Question,
                Answer = f.Answer,
                Category = f.Category.ToString(),
                SortOrder = f.SortOrder,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CmsSectionResponse>> GetPublicCmsByCategoryAsync(CmsSectionCategory category, CancellationToken ct)
    {
        return await _dbContext.CmsSections
            .Where(s => s.IsActive && s.Category == category)
            .OrderBy(s => s.SortOrder)
            .Select(s => new CmsSectionResponse
            {
                Id = s.Id,
                Category = s.Category.ToString(),
                Heading = s.Heading,
                Description = s.Description,
                SortOrder = s.SortOrder
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FaqResponse>> GetAdminFaqsAsync(CancellationToken ct)
    {
        return await _dbContext.Faqs
            .OrderBy(f => f.Category)
            .ThenBy(f => f.SortOrder)
            .Select(f => new FaqResponse
            {
                Id = f.Id,
                Question = f.Question,
                Answer = f.Answer,
                Category = f.Category.ToString(),
                SortOrder = f.SortOrder,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<FaqResponse> CreateFaqAsync(UpsertFaqRequest request, CancellationToken ct)
    {
        var faq = new Faq
        {
            Question = request.Question,
            Answer = request.Answer,
            Category = request.Category,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        _dbContext.Faqs.Add(faq);
        await _dbContext.SaveChangesAsync(ct);
        return new FaqResponse
        {
            Id = faq.Id, Question = faq.Question, Answer = faq.Answer,
            Category = faq.Category.ToString(), SortOrder = faq.SortOrder, CreatedAt = faq.CreatedAt
        };
    }

    public async Task<FaqResponse> UpdateFaqAsync(int id, UpsertFaqRequest request, CancellationToken ct)
    {
        var faq = await _dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq == null) throw new KeyNotFoundException($"FAQ #{id} not found.");

        faq.Question = request.Question;
        faq.Answer = request.Answer;
        faq.Category = request.Category;
        faq.SortOrder = request.SortOrder;
        faq.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(ct);

        return new FaqResponse
        {
            Id = faq.Id, Question = faq.Question, Answer = faq.Answer,
            Category = faq.Category.ToString(), SortOrder = faq.SortOrder, CreatedAt = faq.CreatedAt
        };
    }

    public async Task<bool> DeleteFaqAsync(int id, CancellationToken ct)
    {
        var faq = await _dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq == null) return false;
        _dbContext.Faqs.Remove(faq);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<CmsSectionResponse>> GetAdminCmsSectionsAsync(CancellationToken ct)
    {
        return await _dbContext.CmsSections
            .OrderBy(s => s.Category)
            .ThenBy(s => s.SortOrder)
            .Select(s => new CmsSectionResponse
            {
                Id = s.Id,
                Category = s.Category.ToString(),
                Heading = s.Heading,
                Description = s.Description,
                SortOrder = s.SortOrder
            })
            .ToListAsync(ct);
    }

    public async Task<CmsSectionResponse> CreateCmsSectionAsync(UpsertCmsSectionRequest request, CancellationToken ct)
    {
        var section = new CmsSection
        {
            Category = request.Category,
            Heading = request.Heading,
            Description = request.Description,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        _dbContext.CmsSections.Add(section);
        await _dbContext.SaveChangesAsync(ct);
        return new CmsSectionResponse
        {
            Id = section.Id, Category = section.Category.ToString(),
            Heading = section.Heading, Description = section.Description, SortOrder = section.SortOrder
        };
    }

    public async Task<CmsSectionResponse> UpdateCmsSectionAsync(int id, UpsertCmsSectionRequest request, CancellationToken ct)
    {
        var section = await _dbContext.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section == null) throw new KeyNotFoundException($"CMS Section #{id} not found.");

        section.Category = request.Category;
        section.Heading = request.Heading;
        section.Description = request.Description;
        section.SortOrder = request.SortOrder;
        section.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(ct);

        return new CmsSectionResponse
        {
            Id = section.Id, Category = section.Category.ToString(),
            Heading = section.Heading, Description = section.Description, SortOrder = section.SortOrder
        };
    }

    public async Task<bool> DeleteCmsSectionAsync(int id, CancellationToken ct)
    {
        var section = await _dbContext.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section == null) return false;
        _dbContext.CmsSections.Remove(section);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
