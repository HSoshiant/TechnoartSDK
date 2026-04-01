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
            throw new AuthenticationRequiredException(
                $"API returned {(int)response.StatusCode} for {request.RequestUri}. Please sign in again.");
        }

        return response;
    }

    #endregion Methods
}
