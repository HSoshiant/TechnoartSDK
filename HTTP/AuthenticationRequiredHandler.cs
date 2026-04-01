using System.Net;
using TechnoartSDK.Models;

namespace TechnoartSDK.HTTP;

/// <summary>
/// DelegatingHandler that throws <see cref="AuthenticationRequiredException"/>
/// when the downstream API returns 401 (Unauthorized) or 403 (Forbidden).
/// Register in the HTTP client pipeline so every outgoing request gets the check.
/// </summary>
public class AuthenticationRequiredHandler : DelegatingHandler
{
    #region Methods

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // If no Bearer token was attached, the session/token expired and refresh failed.
            // If a Bearer token WAS attached but the API still rejected it, the user is
            // genuinely not authorised (e.g. account deleted, role revoked).
            var reason = request.Headers.Authorization?.Scheme == "Bearer"
                ? AuthenticationRequiredReason.NotAuthenticated
                : AuthenticationRequiredReason.SessionExpired;

            throw new AuthenticationRequiredException(
                $"API returned {(int)response.StatusCode} for {request.RequestUri}. Please sign in again.",
                reason);
        }

        return response;
    }

    #endregion Methods
}
