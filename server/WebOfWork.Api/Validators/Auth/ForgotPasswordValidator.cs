using FluentValidation;
using WebOfWork.Api.DTOs.Auth;
namespace WebOfWork.Api.Validators.Auth;
public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest> {
    public ForgotPasswordValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress(); }
}