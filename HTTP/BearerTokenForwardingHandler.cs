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
/// cookie's claims. The refreshed token is cached in <see cref="HttpContext.Items"/>
/// because Blazor Server circuits reuse the initial HTTP context whose response has
/// already started (so cookie writes via SignInAsync are not possible).
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

    // Key used to cache the refreshed token in HttpContext.Items for the circuit's lifetime
    private const string CachedTokenKey = "BearerTokenForwarding.CachedApiToken";

    #endregion Fields

    #region Methods

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            // Check the in-memory cache first (refreshed token from earlier in this circuit)
            var token = httpContext.Items[CachedTokenKey] as string
                ?? await httpContext.GetTokenAsync(AuthClaimsConstants.ApiTokenName);

            // Refresh the token if it's missing, expired, or about to expire
            if (string.IsNullOrEmpty(token) || IsExpiredOrNearExpiry(token))
            {
                token = TryRefreshToken(httpContext, token);
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
    /// Regenerates the self-issued JWT from the current user's cookie claims and caches
    /// it in <see cref="HttpContext.Items"/>. This avoids calling SignInAsync which fails
    /// in Blazor Server because the response (WebSocket upgrade) has already started.
    /// </summary>
    private string? TryRefreshToken(HttpContext httpContext, string? oldToken)
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

            // Cache in HttpContext.Items — lives for the Blazor circuit's lifetime
            httpContext.Items[CachedTokenKey] = newToken;

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
