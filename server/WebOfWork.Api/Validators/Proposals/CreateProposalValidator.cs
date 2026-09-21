using FluentValidation;
using WebOfWork.Api.DTOs.Proposals;

namespace WebOfWork.Api.Validators.Proposals;

public class CreateProposalValidator : AbstractValidator<CreateProposalRequest>
{
    public CreateProposalValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.PackageId).NotEmpty();
        RuleFor(x => x.CoverLetter).NotEmpty().MinimumLength(10).MaximumLength(4000);
        RuleFor(x => x.BidAmount).GreaterThan(0);
        RuleFor(x => x.DeliveryDays).GreaterThan(0);
    }
}
