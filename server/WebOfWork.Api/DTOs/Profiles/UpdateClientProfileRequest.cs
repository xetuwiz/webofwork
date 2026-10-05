namespace WebOfWork.Api.DTOs.Profiles;

public record UpdateClientProfileRequest(
    string? FullName,
    string? CompanyName,
    string? Location,
    /// <summary>
    /// Category ids under the key the client actually sends. Interests.jsx posts
    /// <c>{ categories: [1, 2, 3] }</c>; binding only "category_ids" made saving
    /// interests a silent no-op. The alias below is kept for older callers.
    /// </summary>
    List<int>? Categories = null,
    List<int>? CategoryIds = null,
    string? ProfilePhoto = null
)
{
    /// <summary>Ids from whichever key the caller sent.</summary>
    public List<int>? EffectiveCategoryIds => Categories ?? CategoryIds;
}
