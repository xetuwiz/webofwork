using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record ClientProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    string CompanyName,
    string Location,
    DateTime JoinedAt,
    bool IsGoogleAccount,
    int ReviewCount,
    decimal AverageRating,
    IReadOnlyList<int> CategoryIds,
    string? ProfilePhoto = null
);
