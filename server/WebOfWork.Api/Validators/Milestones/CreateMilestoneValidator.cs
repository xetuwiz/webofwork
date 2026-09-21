using System.Collections.Generic;
using FluentValidation;
using WebOfWork.Api.DTOs.Milestones;

namespace WebOfWork.Api.Validators.Milestones;

public class CreateMilestoneListValidator : AbstractValidator<List<CreateMilestoneRequest>>
{
    public CreateMilestoneListValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("At least one milestone is required.")
            .Must(x => x != null && x.Count <= 10).WithMessage("Maximum 10 milestones allowed.");

        RuleForEach(x => x).SetValidator(new CreateMilestoneValidator());
    }
}

public class CreateMilestoneValidator : AbstractValidator<CreateMilestoneRequest>
{
    public CreateMilestoneValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Milestone title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Milestone amount must be greater than 0.");

        RuleFor(x => x.ExpectedDays)
            .GreaterThan(0).WithMessage("Expected days must be greater than 0.");
    }
}

public class SubmitMilestoneValidator : AbstractValidator<SubmitMilestoneRequest>
{
    public SubmitMilestoneValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FileUrl) || !string.IsNullOrWhiteSpace(x.Notes))
            .WithMessage("Either FileUrl or Notes must be provided.");
            
        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}
