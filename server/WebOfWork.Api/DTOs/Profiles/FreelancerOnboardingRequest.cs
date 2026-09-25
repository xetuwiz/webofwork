using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record FreelancerOnboardingRequest(
    string Tagline, string Bio, string Phone, string ExperienceLevel, int PrimaryCategory,
    List<string> Skills, Dictionary<string, OnboardingPackageRequest> Packages,
    List<OnboardingPortfolioRequest> Portfolios, PaymentDetailsRequest PaymentDetails);
public record OnboardingPackageRequest(decimal Price, int DeliveryTime, string Description);
public record OnboardingPortfolioRequest(string Title, string Description, string ImageUrl);
public record PaymentDetailsRequest(string PaypalEmail);
