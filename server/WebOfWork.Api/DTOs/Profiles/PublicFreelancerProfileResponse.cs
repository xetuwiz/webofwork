using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record PublicFreelancerProfileResponse(
    Guid Id,
    string FullName,
    string Tagline,
    string Bio,
    bool IsPhoneVerified,
    int? CategoryId,
    string ExperienceLevel,
    bool Availability,
    decimal AverageRating,
    int ReviewCount,
    DateTime CreatedAt,
    List<ProfileSkillDto> Skills,
    List<ProfilePackageDto> Packages,
    List<PortfolioDto> Portfolios
);
