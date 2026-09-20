using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record FreelancerOnboardingRequest(
    string Tagline,
    string Bio,
    string Phone,
    string ExperienceLevel,
    int PrimaryCategory,
    List<OnboardingSkillDto> Skills,
    List<OnboardingPackageDto> Packages
);

public record OnboardingSkillDto(int? SkillId, string CustomName);
public record OnboardingPackageDto(string PackageType, decimal Price, int DeliveryDays, string Description);
