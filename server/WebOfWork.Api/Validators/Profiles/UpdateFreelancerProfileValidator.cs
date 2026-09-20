using FluentValidation;
using WebOfWork.Api.DTOs.Profiles;

namespace WebOfWork.Api.Validators.Profiles;

public class UpdateFreelancerProfileValidator : AbstractValidator<UpdateFreelancerProfileRequest>
{
    public UpdateFreelancerProfileValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(255);
        RuleFor(x => x.Tagline).MaximumLength(100);
        RuleFor(x => x.Bio).MaximumLength(500);
        RuleFor(x => x.ExperienceLevel).Must(x => x == null || x == "beginner" || x == "intermediate" || x == "expert");
    }
}
