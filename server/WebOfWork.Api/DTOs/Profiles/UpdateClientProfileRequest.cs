namespace WebOfWork.Api.DTOs.Profiles;

public record UpdateClientProfileRequest(
    string? FullName,
    string? CompanyName,
    string? Location,
    List<int>? CategoryIds = null,
    string? ProfilePhoto = null
);
