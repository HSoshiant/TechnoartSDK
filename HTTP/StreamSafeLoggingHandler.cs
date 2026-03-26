using Microsoft.Extensions.Logging;

namespace TechnoartSDK.HTTP;

/// <summary>
/// HTTP logging handler safe for all response types, including SSE and NDJSON streaming.
/// Logs the request, response status, and full response body for every call.
/// A <see cref="TeeStream"/> captures bytes as the real consumer reads them, then logs
/// the collected body when the stream is disposed — so the response stream is never
/// pre-read or buffered before the caller receives it.
/// </summary>
public class StreamSafeLoggingHandler(ILogger<StreamSafeLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        logger.LogInformation("HTTP Request: {Method} {Uri}", request.Method, request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken);

        logger.LogInformation("HTTP Response: {StatusCode}", (int)response.StatusCode);

        // Wrap the response body in a TeeStream so the consumer reads normally while bytes
        // are captured in parallel. The full body is logged when the stream is disposed.
        var originalContent = response.Content;
        var originalStream = await originalContent.ReadAsStreamAsync(cancellationToken);

        var teeStream = new TeeStream(originalStream,
            body => logger.LogInformation("Response Body: {Body}", body));

        var newContent = new StreamContent(teeStream);
        // Copy all content headers (Content-Type, Content-Length, etc.) to the replacement content
        foreach (var header in originalContent.Headers)
            newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

        response.Content = newContent;
        return response;
    }
}
