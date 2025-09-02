using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TechnoartSDK.HTTP;

public class RequestBodyLoggingMiddleware(ILogger<RequestBodyLoggingMiddleware> logger) :IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;

        // Request body can read multiple times
        context.Request.EnableBuffering();

        if (context.Request.Body.CanRead && (method == HttpMethods.Put || method == HttpMethods.Post))
        {
            // Leave stream open for next middleware
            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 512,
                leaveOpen: true
                );
            var requestBody = await reader.ReadToEndAsync();

            // Resetting the stream reader
            context.Request.Body.Position = 0;

            // write request body to App Insights
            logger.LogInformation("Request Body: {RequestBody}", requestBody);
            //var requestToTelemetry = context.Features.Get<RequestTelemetry>();
            //requestToTelemetry?.Properties.Add("RequestBody", requestBody);
        }

        await next(context);
    }
}
