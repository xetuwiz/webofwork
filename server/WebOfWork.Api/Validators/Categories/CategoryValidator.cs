using FluentValidation;
using WebOfWork.Api.DTOs.Categories;

namespace WebOfWork.Api.Validators.Categories;

public class CategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters.");
    }
}
