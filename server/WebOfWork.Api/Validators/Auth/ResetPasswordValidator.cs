using FluentValidation;
using WebOfWork.Api.DTOs.Auth;
namespace WebOfWork.Api.Validators.Auth;
public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest> {
    public ResetPasswordValidator() {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}