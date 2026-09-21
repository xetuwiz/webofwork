using FluentValidation;

namespace WebOfWork.Api.Validators.Revisions;

public class RequestRevisionValidator : AbstractValidator<WebOfWork.Api.DTOs.Revisions.RequestRevisionRequest>
{
    public RequestRevisionValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters.");
    }
}
