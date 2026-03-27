using System.Security.Claims;

namespace TechnoartSDK.Extensions;

/// <summary>
/// Standard claim type constants and configuration keys used in self-issued JWTs and user resolution.
/// </summary>
public static class AuthClaimsConstants
{
    public const string Subject = ClaimTypes.NameIdentifier;
    public const string Email = ClaimTypes.Email;
    public const string Name = ClaimTypes.Name;
    public const string Picture = "picture";
    public const string Provider = "provider";
    public const string ProviderId = "provider_id";

    /// <summary>Configuration section key for the shared symmetric signing key.</summary>
    public const string TokenSigningKeyConfig = "Authentication:TokenSigningKey";

    /// <summary>Configuration section key for the list of OAuth providers.</summary>
    public const string ProvidersConfig = "Authentication:Providers";

    /// <summary>Name of the authentication token stored in auth properties and read by the handler.</summary>
    public const string ApiTokenName = "api_token";

    /// <summary>Default sign-in page path used by cookie authentication.</summary>
    public const string SignInPath = "/auth/signin";

    /// <summary>Login endpoint path that initiates the OAuth challenge for a specific provider.</summary>
    public const string LoginPath = "/auth/login";

    /// <summary>Logout endpoint path that signs out the cookie and redirects to the landing page.</summary>
    public const string LogoutPath = "/auth/logout";

    /// <summary>Provider name for Google OAuth.</summary>
    public const string GoogleProvider = "google";

    /// <summary>Provider name for Microsoft OAuth.</summary>
    public const string MicrosoftProvider = "microsoft";

    /// <summary>OAuth callback path for Google sign-in.</summary>
    public const string GoogleCallbackPath = "/signin-google";

    /// <summary>OAuth callback path for Microsoft sign-in.</summary>
    public const string MicrosoftCallbackPath = "/signin-microsoft";

    /// <summary>Provider name for GitHub OAuth.</summary>
    public const string GitHubProvider = "github";

    /// <summary>OAuth callback path for GitHub sign-in.</summary>
    public const string GitHubCallbackPath = "/signin-github";
}
