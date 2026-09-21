using FluentValidation;
using WebOfWork.Api.DTOs.Invitations;

namespace WebOfWork.Api.Validators.Invitations;

public class CreateInvitationValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationValidator()
    {
        RuleFor(x => x.FreelancerId).NotEmpty();
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.PackageId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(2000);
    }
}
