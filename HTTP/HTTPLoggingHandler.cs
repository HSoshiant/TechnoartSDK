using Microsoft.Extensions.Logging;

namespace TechnoartSDK.HTTP;

public class HTTPLoggingHandler(ILogger<HTTPLoggingHandler> logger) :DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        logger.LogInformation("HTTP Request: {method} {url}", request.Method, request.RequestUri);

        if (request.Content != null)
        {
            var requestBody = await request.Content.ReadAsStringAsync();
            //logger.LogInformation("Request Body: {body}", requestBody);
        }

        var response = await base.SendAsync(request, cancellationToken);
        logger.LogInformation("HTTP Response: {statusCode}", response.StatusCode);

        if (response.Content != null)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            logger.LogInformation("Response Body: {body}", responseBody);
        }
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("HTTP Request failed with status code {statusCode}", response.StatusCode);
        }

        return response;
    }

}
