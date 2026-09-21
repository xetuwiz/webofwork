using FluentValidation;
using WebOfWork.Api.DTOs.Refunds;

namespace WebOfWork.Api.Validators.Refunds;

public class RefundPaymentValidator : AbstractValidator<RefundPaymentRequest>
{
    public RefundPaymentValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}
