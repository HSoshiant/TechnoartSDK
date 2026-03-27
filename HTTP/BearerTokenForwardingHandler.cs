using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TechnoartSDK.Extensions;

namespace TechnoartSDK.HTTP;

/// <summary>
/// DelegatingHandler that reads the self-issued API token from the current HTTP context
/// and attaches it as a Bearer Authorization header on outgoing requests.
/// Designed for Blazor Server / SSR apps where the Web host forwards a self-issued JWT
/// (generated after OAuth sign-in) to a downstream API.
/// </summary>
public class BearerTokenForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<BearerTokenForwardingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var token = await httpContext.GetTokenAsync(AuthClaimsConstants.ApiTokenName);
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
}
