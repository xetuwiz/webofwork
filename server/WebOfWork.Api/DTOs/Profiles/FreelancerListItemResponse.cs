using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record FreelancerListItemResponse(
    Guid Id,
    string FullName,
    string Tagline,
    string Bio,
    int? CategoryId,
    string ExperienceLevel,
    bool Availability,
    decimal AverageRating,
    int ReviewCount,
    List<SkillDto> Skills,
    decimal StartingPrice
);
