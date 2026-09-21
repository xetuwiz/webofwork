using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Platform;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IContentService
{
    Task<GlobalSettings> GetGlobalSettingsAsync(CancellationToken cancellationToken = default);
    Task<GlobalSettings> UpdateGlobalSettingsAsync(UpdateGlobalSettingsRequest request, CancellationToken cancellationToken = default);
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
            // Fallback in case seed failed, though configuration handles it.
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
}
