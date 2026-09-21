using FluentValidation;
using WebOfWork.Api.DTOs.Projects;

namespace WebOfWork.Api.Validators.Projects;

public class CreateProjectValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(20);
        RuleFor(x => x.PricingType).InclusiveBetween(1, 2);
        RuleFor(x => x.ExpectedDays).GreaterThan(0);

        // Fixed pricing invariant
        RuleFor(x => x.FixedPrice)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.PricingType == 1)
            .WithMessage("Fixed price is required and must be greater than 0 for fixed pricing.");

        RuleFor(x => x.FixedPrice)
            .Null()
            .When(x => x.PricingType != 1)
            .WithMessage("Fixed price must be null when pricing type is not fixed.");

        // Range pricing invariants
        RuleFor(x => x.MinPrice)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.PricingType == 2)
            .WithMessage("Min price is required and must be greater than 0 for range pricing.");

        RuleFor(x => x.MaxPrice)
            .NotNull()
            .GreaterThan(x => x.MinPrice ?? 0)
            .When(x => x.PricingType == 2)
            .WithMessage("Max price is required and must be greater than min price for range pricing.");

        RuleFor(x => x.MinPrice)
            .Null()
            .When(x => x.PricingType != 2)
            .WithMessage("Min price must be null when pricing type is not range.");

        RuleFor(x => x.MaxPrice)
            .Null()
            .When(x => x.PricingType != 2)
            .WithMessage("Max price must be null when pricing type is not range.");
    }
}

public class UpdateProjectValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(20);
        RuleFor(x => x.ExpectedDays).GreaterThan(0);
    }
}
