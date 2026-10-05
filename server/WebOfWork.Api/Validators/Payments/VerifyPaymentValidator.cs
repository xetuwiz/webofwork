using FluentValidation;
using WebOfWork.Api.DTOs.Payments;

namespace WebOfWork.Api.Validators.Payments;

public class VerifyPaymentValidator : AbstractValidator<VerifyPaymentRequest>
{
    public VerifyPaymentValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.PayPalOrderId).NotEmpty();
        RuleFor(x => x.PayPalAmountUsd).GreaterThan(0)
            .WithMessage("PayPal USD amount must be greater than zero.");
    }
}
