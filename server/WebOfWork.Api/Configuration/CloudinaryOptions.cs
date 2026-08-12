namespace WebOfWork.Api.Configuration;

/// <summary>
/// CloudinaryDotNet SDK options.
/// Confirmed in Grolance settings.py: CLOUDINARY_STORAGE (CloudName, ApiKey, ApiSecret, UploadPreset).
/// </summary>
public sealed class CloudinaryOptions
{
    public const string Section = "Cloudinary";

    public string CloudName    { get; init; } = string.Empty;
    public string ApiKey       { get; init; } = string.Empty;
    public string ApiSecret    { get; init; } = string.Empty;
    public string UploadPreset { get; init; } = string.Empty;
    public bool   Secure       { get; init; } = true;
}
