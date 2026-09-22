using FluentValidation;
using WebOfWork.Api.DTOs.Content;

namespace WebOfWork.Api.Validators.Content;

public class UpsertFaqRequestValidator : AbstractValidator<UpsertFaqRequest>
{
    public UpsertFaqRequestValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required.")
            .MaximumLength(500).WithMessage("Question must be at most 500 characters.");

        RuleFor(x => x.Answer)
            .NotEmpty().WithMessage("Answer is required.")
            .MaximumLength(8000).WithMessage("Answer must be at most 8000 characters.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid FAQ category.");
    }
}

public class UpsertCmsSectionRequestValidator : AbstractValidator<UpsertCmsSectionRequest>
{
    public UpsertCmsSectionRequestValidator()
    {
        RuleFor(x => x.Heading)
            .NotEmpty().WithMessage("Heading is required.")
            .MaximumLength(255).WithMessage("Heading must be at most 255 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(8000).WithMessage("Description must be at most 8000 characters.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid CMS section category.");
    }
}
