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
    /// <summary>
    /// Names, not objects. the reference FreelancerListingSerializer.get_skills returns
    /// <c>values_list('custom_name', flat=True)</c>, and FreelancerCard.jsx renders
    /// each entry straight into a &lt;span&gt; — objects produced [object Object].
    /// </summary>
    List<string> Skills,
    decimal StartingPrice,
    /// <summary>
    /// The user's avatar.
    ///
    /// FindTalent.jsx has always passed <c>imageUrl={freelancer.profile_photo}</c>
    /// to FreelancerCard, but this record had no such field, so every avatar on
    /// /find-talents was missing and the card fell back to initials for all of
    /// them. <c>ParityFreelancerListItemResponse</c> already carried a
    /// ProfilePhoto, so the reference shape has always included one here.
    /// </summary>
    string? ProfilePhoto
);
