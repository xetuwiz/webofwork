using FluentValidation;

namespace WebOfWork.Api.DTOs.Platform;

public record UpdateGlobalSettingsRequest(
    string? SupportEmail,
    decimal? CommissionPercentage,
    string? PayPalEmail,
    decimal? MinBriefConfidence,
    decimal? ProposalScreeningThreshold,
    decimal? MatchScoreThreshold,
    string? DefaultCurrency
);

public class UpdateGlobalSettingsRequestValidator : AbstractValidator<UpdateGlobalSettingsRequest>
{
    public UpdateGlobalSettingsRequestValidator()
    {
        RuleFor(x => x.SupportEmail)
            .EmailAddress().When(x => x.SupportEmail != null);

        RuleFor(x => x.PayPalEmail)
            .EmailAddress().When(x => x.PayPalEmail != null);

        RuleFor(x => x.CommissionPercentage)
            .InclusiveBetween(0, 100).When(x => x.CommissionPercentage.HasValue);

        RuleFor(x => x.MinBriefConfidence)
            .InclusiveBetween(0, 1).When(x => x.MinBriefConfidence.HasValue);

        RuleFor(x => x.ProposalScreeningThreshold)
            .InclusiveBetween(0, 1).When(x => x.ProposalScreeningThreshold.HasValue);

        RuleFor(x => x.MatchScoreThreshold)
            .InclusiveBetween(0, 1).When(x => x.MatchScoreThreshold.HasValue);

        // The controller will also check this, but we can fail fast here too if they send a different currency.
        RuleFor(x => x.DefaultCurrency)
            .Must(c => string.Equals(c, "LKR", StringComparison.OrdinalIgnoreCase))
            .When(x => x.DefaultCurrency != null)
            .WithMessage("DefaultCurrency is immutable and must remain LKR.");
    }
}
