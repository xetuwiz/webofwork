using FluentValidation;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Integrations.Sms;

namespace WebOfWork.Api.Validators.Profiles;

/// <summary>
/// Validates the phone-verification requests. These requests were previously
/// unvalidated, so an invalid number reached the service and surfaced as an
/// unhandled ArgumentException (HTTP 500) instead of a 400 with a useful message.
/// </summary>
public class PhoneOtpValidator : AbstractValidator<PhoneOtpRequest>
{
    public PhoneOtpValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Must((string p) => SriLankanPhone.IsValid(p))
            .WithMessage(SriLankanPhone.ErrorMessage);
    }
}

public class VerifyPhoneOtpValidator : AbstractValidator<VerifyPhoneOtpRequest>
{
    public VerifyPhoneOtpValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Must((string p) => SriLankanPhone.IsValid(p))
            .WithMessage(SriLankanPhone.ErrorMessage);

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("OTP must be 6 digits.");
    }
}

public class FreelancerOnboardingValidator : AbstractValidator<FreelancerOnboardingRequest>
{
    public FreelancerOnboardingValidator()
    {
        RuleFor(x => x.Tagline).NotEmpty().Length(10, 80);
        RuleFor(x => x.Bio).NotEmpty().Length(50, 500);
        // Was Length(10), which only accepted the raw "0779091086" form and
        // rejected the equally valid "+94779091086" and "779091086" that
        // the SMS normalizer accepts. Validate by normalizing instead so
        // every accepted shape works.
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Must((string p) => SriLankanPhone.IsValid(p))
            .WithMessage(SriLankanPhone.ErrorMessage);
        RuleFor(x => x.ExperienceLevel).Must(x => x == "beginner" || x == "intermediate" || x == "expert");
        RuleFor(x => x.PrimaryCategory).GreaterThan(0);
        RuleFor(x => x.Skills).NotEmpty().Must(x => x != null && x.Count >= 1 && x.Count <= 10).WithMessage("Must provide between 1 and 10 skills.");
        RuleFor(x => x.Packages).NotEmpty().Must(x => x != null && x.Count >= 1).WithMessage("Must provide at least one package.");
        // Delivery days were silently stored as 0: the client sent the camelCase
        // key "deliveryTime", but the API applies SnakeCaseLower globally, so the
        // value never bound and the int defaulted to 0. The request still returned
        // 201, so the days vanished with no error. Contract due dates are derived
        // from delivery_days, so a 0 produced an already-due contract.
        RuleForEach(x => x.Packages)
            .ChildRules(pkg => pkg.RuleFor(t => t.Value.DeliveryTime)
                .GreaterThan(0).WithMessage("Delivery time must be at least 1 day."));
        RuleForEach(x => x.Packages)
            .ChildRules(pkg => pkg.RuleFor(t => t.Value.Price)
                .GreaterThan(0).WithMessage("Package price must be greater than 0."));
    }
}
