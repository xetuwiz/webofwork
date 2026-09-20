using FluentValidation;
using WebOfWork.Api.DTOs.Categories;

namespace WebOfWork.Api.Validators.Categories;

public class SkillValidator : AbstractValidator<CreateSkillRequest>
{
    public SkillValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Skill name is required.")
            .MaximumLength(100).WithMessage("Skill name must not exceed 100 characters.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Category ID must be valid.");
    }
}
