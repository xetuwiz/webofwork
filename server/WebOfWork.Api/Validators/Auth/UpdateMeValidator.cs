using FluentValidation;
using WebOfWork.Api.DTOs.Auth;
namespace WebOfWork.Api.Validators.Auth;
public class UpdateMeValidator : AbstractValidator<UpdateMeRequest> {
    public UpdateMeValidator() {
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email != null);
        RuleFor(x => x.FullName).MaximumLength(255).When(x => x.FullName != null);
    }
}