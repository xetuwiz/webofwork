using FluentValidation;
using WebOfWork.Api.DTOs.Refunds;

namespace WebOfWork.Api.Validators.Refunds;

public class RefundPaymentValidator : AbstractValidator<RefundPaymentRequest>
{
    public RefundPaymentValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount.HasValue);
    }
}
