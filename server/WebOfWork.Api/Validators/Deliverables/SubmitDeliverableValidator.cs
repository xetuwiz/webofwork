using FluentValidation;

namespace WebOfWork.Api.Validators.Deliverables;

public class SubmitDeliverableValidator : AbstractValidator<WebOfWork.Api.DTOs.Deliverables.SubmitDeliverableRequest>
{
    public SubmitDeliverableValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.");

        RuleFor(x => x.FileUrl)
            .NotEmpty().When(x => string.IsNullOrWhiteSpace(x.LinkUrl))
            .WithMessage("Either a File URL or a Link URL must be provided.");

        RuleFor(x => x.LinkUrl)
            .NotEmpty().When(x => string.IsNullOrWhiteSpace(x.FileUrl))
            .WithMessage("Either a File URL or a Link URL must be provided.");
    }
}
