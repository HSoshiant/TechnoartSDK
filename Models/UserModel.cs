using Newtonsoft.Json;

namespace TechnoartSDK.Models;

/// <summary>
/// Represents an authenticated user persisted in the database.
/// </summary>
public record UserModel : RepositoryModel
{
    /// <summary>
    /// The authentication provider name (e.g. "Google", "Microsoft", "GitHub").
    /// </summary>
    [JsonProperty("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The unique user identifier from the authentication provider (e.g. the "sub" claim).
    /// </summary>
    [JsonProperty("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// User's email address.
    /// </summary>
    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's display name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL of the user's profile picture.
    /// </summary>
    [JsonProperty("pictureUrl")]
    public string PictureUrl { get; set; } = string.Empty;
}
