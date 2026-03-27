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

    /// <summary>List of configured OAuth providers.</summary>
    public List<AuthProviderConfig> Providers { get; set; } = [];
}
