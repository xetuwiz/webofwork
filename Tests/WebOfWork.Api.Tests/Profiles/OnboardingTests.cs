using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using WebOfWork.Api.DTOs.Profiles;
using WebOfWork.Api.Validators.Profiles;

namespace WebOfWork.Api.Tests.Profiles;

public class OnboardingTests
{
    [Fact]
    public void Validator_ShouldFail_WhenSkillCountIsLessThanOne()
    {
        // The DTO shape changed: skills are plain strings, packages are a
        // dictionary keyed by name (there is no `Key` on the value any more, so
        // "starter" moves out of the constructor and into the key), and the
        // record gained a required `Portfolios` list and a `PaymentDetails`.
        var request = new FreelancerOnboardingRequest(
            "Tag", "Bio", "0771234567", "expert", 1,
            new List<string>(),
            new Dictionary<string, OnboardingPackageRequest>
            {
                ["starter"] = new(1000, 1, "test")
            },
            new List<OnboardingPortfolioRequest>(),
            new PaymentDetailsRequest("paypal@example.com")
        );
        var validator = new FreelancerOnboardingValidator();
        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Skills");
    }
}
