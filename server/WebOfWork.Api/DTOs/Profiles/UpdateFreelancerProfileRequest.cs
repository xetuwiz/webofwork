namespace WebOfWork.Api.DTOs.Profiles;

public record UpdateFreelancerProfileRequest(
    string? FullName,
    string? Tagline,
    string? Bio,
    string? ExperienceLevel,
    bool? Availability
);
