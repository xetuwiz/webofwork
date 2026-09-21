using FluentValidation;
using WebOfWork.Api.DTOs.Payouts;

namespace WebOfWork.Api.Validators.Payouts;

public class ReleasePayoutValidator : AbstractValidator<ReleasePayoutRequest>
{
    public ReleasePayoutValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
    }
}
