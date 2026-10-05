namespace WebOfWork.Api.DTOs.Profiles;

public record UpdateFreelancerProfileRequest(
    string? FullName,
    string? Tagline,
    string? Bio,
    string? ExperienceLevel,
    bool? Availability,
    // Packages/skills were sent by ProfileEdit but had no DTO fields, so the
    // model binder dropped them: the save returned 200 but package price and
    // delivery days never changed. Now bound and applied by the service.
    Dictionary<string, PackageUpdateDto>? Packages,
    List<string>? Skills,
    // A freelancer's category was set at onboarding and could never be changed
    // afterwards: not in this DTO, not in the service, and no control in the edit
    // form. Clients filter the talent list by category, so a wrong category made a
    // freelancer invisible to that whole filter.
    int? CategoryId
);

public record PackageUpdateDto(decimal Price, int DeliveryDays, string? Description);
