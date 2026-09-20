using FluentValidation;
using WebOfWork.Api.DTOs.Briefs;

namespace WebOfWork.Api.Validators.Briefs;

public class CreateBriefValidator : AbstractValidator<CreateBriefRequest>
{
    public CreateBriefValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Brief text is required.")
            .MinimumLength(10).WithMessage("Brief text must be at least 10 characters.");
            
        RuleFor(x => x.BudgetHint)
            .GreaterThan(0).When(x => x.BudgetHint.HasValue)
            .WithMessage("Budget hint must be greater than 0.");
    }
}
