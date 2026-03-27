namespace TechnoartSDK.Models;

/// <summary>
/// Configuration for an external OAuth authentication provider.
/// Bind from the <c>Authentication:Providers</c> configuration section.
/// </summary>
public record AuthProviderConfig
{
    /// <summary>
    /// Provider display name used as the authentication scheme (e.g. "Google", "Microsoft").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client ID from the provider's developer console.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret from the provider's developer console.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
