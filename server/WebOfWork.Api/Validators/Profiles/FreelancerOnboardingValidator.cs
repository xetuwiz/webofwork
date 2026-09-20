using FluentValidation;
using WebOfWork.Api.DTOs.Profiles;

namespace WebOfWork.Api.Validators.Profiles;

public class FreelancerOnboardingValidator : AbstractValidator<FreelancerOnboardingRequest>
{
    public FreelancerOnboardingValidator()
    {
        RuleFor(x => x.Tagline).NotEmpty().Length(10, 80);
        RuleFor(x => x.Bio).NotEmpty().Length(50, 500);
        RuleFor(x => x.Phone).NotEmpty().Length(10);
        RuleFor(x => x.ExperienceLevel).Must(x => x == "beginner" || x == "intermediate" || x == "expert");
        RuleFor(x => x.PrimaryCategory).GreaterThan(0);
        RuleFor(x => x.Skills).NotEmpty().Must(x => x != null && x.Count >= 1 && x.Count <= 10).WithMessage("Must provide between 1 and 10 skills.");
        RuleFor(x => x.Packages).NotEmpty().Must(x => x != null && x.Count >= 1).WithMessage("Must provide at least one package.");
    }
}
