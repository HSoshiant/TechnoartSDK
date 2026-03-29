using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnoartSDK.Extensions;
using TechnoartSDK.Models;

namespace TechnoartSDK.HTTP;

/// <summary>
/// DelegatingHandler that reads the self-issued API token from the current HTTP context
/// and attaches it as a Bearer Authorization header on outgoing requests.
/// Designed for Blazor Server / SSR apps where the Web host forwards a self-issued JWT
/// (generated after OAuth sign-in) to a downstream API.
/// When the JWT is expired or near expiry, it is automatically regenerated from the
/// cookie's claims so the user does not have to re-authenticate.
/// </summary>
public class BearerTokenForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ExternalAuthOptions> authOptions,
    ILogger<BearerTokenForwardingHandler> logger) : DelegatingHandler
{
    #region Fields

    // Regenerate the token when it has less than this much time remaining
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromHours(1);

    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    #endregion Fields

    #region Methods

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var token = await httpContext.GetTokenAsync(AuthClaimsConstants.ApiTokenName);

            // Refresh the token if it's missing, expired, or about to expire
            if (string.IsNullOrEmpty(token) || IsExpiredOrNearExpiry(token))
            {
                token = await TryRefreshTokenAsync(httpContext, token);
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                logger.LogWarning("No {TokenName} found in the current HTTP context for outgoing request to {Uri}",
                    AuthClaimsConstants.ApiTokenName, request.RequestUri);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Checks whether a JWT is expired or within the refresh threshold.
    /// </summary>
    private bool IsExpiredOrNearExpiry(string token)
    {
        try
        {
            var jwt = TokenHandler.ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow.Add(RefreshThreshold);
        }
        catch
        {
            // Malformed token — treat as expired so it gets regenerated
            return true;
        }
    }

    /// <summary>
    /// Regenerates the self-issued JWT from the current user's cookie claims and updates
    /// the stored authentication token so subsequent requests use the fresh token.
    /// </summary>
    private async Task<string?> TryRefreshTokenAsync(HttpContext httpContext, string? oldToken)
    {
        try
        {
            var user = httpContext.User;
            if (user.Identity is not { IsAuthenticated: true })
            {
                return null;
            }

            var signingKey = authOptions.Value.TokenSigningKey;
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                logger.LogError("Cannot refresh API token — TokenSigningKey is not configured");
                return oldToken;
            }

            var newToken = AuthExtensions.GenerateApiToken(user.Claims, signingKey);

            // Update the stored token in authentication properties so it persists across requests
            var authResult = await httpContext.AuthenticateAsync();
            if (authResult.Succeeded && authResult.Properties is not null)
            {
                var tokens = authResult.Properties.GetTokens().ToList();
                var existing = tokens.Find(t => t.Name == AuthClaimsConstants.ApiTokenName);
                if (existing is not null)
                {
                    existing.Value = newToken;
                }
                else
                {
                    tokens.Add(new AuthenticationToken { Name = AuthClaimsConstants.ApiTokenName, Value = newToken });
                }

                authResult.Properties.StoreTokens(tokens);
                // Re-sign-in to persist the updated properties into the cookie
                await httpContext.SignInAsync(authResult.Principal!, authResult.Properties);
            }

            logger.LogInformation("Refreshed expired API token for user {UserId}",
                user.FindFirst(AuthClaimsConstants.Subject)?.Value);

            return newToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh API token");
            return oldToken;
        }
    }

    #endregion Methods
}
