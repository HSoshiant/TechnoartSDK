namespace TechnoartSDK.Models;

/// <summary>
/// Strongly-typed options for the self-issued JWT authentication system.
/// Bind from the <c>Authentication</c> configuration section.
/// </summary>
public class ExternalAuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication";

    /// <summary>Shared symmetric key used to sign and validate self-issued JWTs.</summary>
    public string TokenSigningKey { get; set; } = string.Empty;

    /// <summary>JWT token expiry duration in hours.</summary>
    public int TokenExpiryHours { get; set; } = 12;

    /// <summary>Token refresh threshold in hours — regenerate when less than this time remaining.</summary>
    public int TokenRefreshThresholdHours { get; set; } = 1;

    /// <summary>List of configured OAuth providers.</summary>
    public List<AuthProviderConfig> Providers { get; set; } = [];
}
