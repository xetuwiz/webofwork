using FluentValidation;
using WebOfWork.Api.DTOs.Auth;
namespace WebOfWork.Api.Validators.Auth;
public class SendOtpValidator : AbstractValidator<SendOtpRequest> {
    public SendOtpValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress(); }
}