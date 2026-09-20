using FluentValidation;
using WebOfWork.Api.DTOs.Profiles;

namespace WebOfWork.Api.Validators.Profiles;

public class UpdateClientProfileValidator : AbstractValidator<UpdateClientProfileRequest>
{
    public UpdateClientProfileValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(255);
        RuleFor(x => x.CompanyName).MaximumLength(255);
        RuleFor(x => x.Location).MaximumLength(255);
    }
}
