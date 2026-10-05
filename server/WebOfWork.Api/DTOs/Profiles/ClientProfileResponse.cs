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
    /// <summary>
    /// Category NAMES, not ids. the reference uses StringRelatedField(many=True); the ids
    /// live on the entity. ProfileOverview.jsx and Interests.jsx both read this key.
    /// </summary>
    IReadOnlyList<string> Categories,
    string? ProfilePhoto = null
);
