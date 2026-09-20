using FluentValidation;
using WebOfWork.Api.DTOs.Auth;
namespace WebOfWork.Api.Validators.Auth;
public class GoogleLoginValidator : AbstractValidator<GoogleLoginRequest> {
    public GoogleLoginValidator() {
        RuleFor(x => x.IdToken).NotEmpty();
        RuleFor(x => x.Role).InclusiveBetween(1, 3);
    }
}