using FluentValidation;
using WebOfWork.Api.DTOs.Projects;

namespace WebOfWork.Api.Validators.Projects;

public class CreateProjectValidator : AbstractValidator<CreateProjectRequest>
{
    // PricingType is now a string ("fixed"/"range"), normalised by
    // FlexiblePricingTypeConverter, so compare against the names not 1/2.
    private static bool IsFixed(CreateProjectRequest x) =>
        string.Equals(x.PricingType, "fixed", StringComparison.OrdinalIgnoreCase);
    private static bool IsRange(CreateProjectRequest x) =>
        string.Equals(x.PricingType, "range", StringComparison.OrdinalIgnoreCase);

    public CreateProjectValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(20);
        RuleFor(x => x.PricingType)
            .Must(p => IsFixedFor(p))
            .WithMessage("Pricing type must be 'fixed' or 'range'.");
        RuleFor(x => x.DeliveryDays).GreaterThan(0);

        // Fixed pricing invariant
        RuleFor(x => x.FixedPrice)
            .NotNull()
            .GreaterThan(0)
            .When(IsFixed)
            .WithMessage("Fixed price is required and must be greater than 0 for fixed pricing.");

        RuleFor(x => x.FixedPrice)
            .Null()
            .When(x => !IsFixed(x))
            .WithMessage("Fixed price must be null when pricing type is not fixed.");

        // Range pricing invariants
        RuleFor(x => x.MinPrice)
            .NotNull()
            .GreaterThan(0)
            .When(IsRange)
            .WithMessage("Min price is required and must be greater than 0 for range pricing.");

        RuleFor(x => x.MaxPrice)
            .NotNull()
            .GreaterThan(x => x.MinPrice ?? 0)
            .When(IsRange)
            .WithMessage("Max price is required and must be greater than min price for range pricing.");

        RuleFor(x => x.MinPrice)
            .Null()
            .When(x => !IsRange(x))
            .WithMessage("Min price must be null when pricing type is not range.");

        RuleFor(x => x.MaxPrice)
            .Null()
            .When(x => !IsRange(x))
            .WithMessage("Max price must be null when pricing type is not range.");
    }

    private static bool IsFixedFor(string? p) =>
        string.Equals(p, "fixed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(p, "range", StringComparison.OrdinalIgnoreCase);
}

public class UpdateProjectValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(20);
        RuleFor(x => x.DeliveryDays).GreaterThan(0);
    }
}
