using FluentValidation;
using WebOfWork.Api.DTOs.Invitations;

namespace WebOfWork.Api.Validators.Invitations;

public class CreateInvitationValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationValidator()
    {
        // The frontend sends the short names (freelancer/project/package) while other callers
        // send the _id-suffixed variants, so accept either rather than requiring one spelling.
        RuleFor(x => x.Freelancer).NotEmpty().When(x => x.FreelancerId == Guid.Empty);
        RuleFor(x => x.FreelancerId).NotEmpty().When(x => x.Freelancer == Guid.Empty);
        RuleFor(x => x.Project).GreaterThan(0).When(x => x.ProjectId == 0);
        RuleFor(x => x.ProjectId).GreaterThan(0).When(x => x.Project == 0);
        RuleFor(x => x.Package).NotEmpty().When(x => x.PackageId == Guid.Empty);
        RuleFor(x => x.PackageId).NotEmpty().When(x => x.Package == Guid.Empty);
        RuleFor(x => x.Message).MaximumLength(2000);
    }
}
