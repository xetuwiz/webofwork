using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record FreelancerProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    string Tagline,
    string Bio,
    string Phone,
    bool IsPhoneVerified,
    int? CategoryId,
    string ExperienceLevel,
    bool Availability,
    decimal AverageRating,
    int ReviewCount,
    DateTime CreatedAt,
    List<ProfileSkillDto> Skills,
    List<ProfilePackageDto> Packages,
    List<PortfolioDto> Portfolios,
    int CompletedProjectsCount = 0,
    IReadOnlyList<object> Reviews = null!
);

public record ProfileSkillDto(Guid Id, int? SkillId, string CustomName);
public record ProfilePackageDto(Guid Id, string PackageType, decimal Price, int DeliveryDays, string Description);
public record PortfolioDto(Guid Id, string Title, string Description, string ImageUrl, DateTime CreatedAt);
