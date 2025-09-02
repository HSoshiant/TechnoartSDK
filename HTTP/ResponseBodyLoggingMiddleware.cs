//namespace Fame.ApiService.Common;

//public class ResponseBodyLoggingMiddleware :IMiddleware
//{
//    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
//    {
//        var originalBodyStream = context.Response.Body;
//        try
//        {
//            using var copyShadowStream = new MemoryStream();
//            var dualStream = new DualStream(originalBodyStream, copyShadowStream);
//            context.Response.Body = dualStream;

//            await next(context);

//            // Now the memory stream contains the response body
//            copyShadowStream.Position = 0;
//            var reader = new StreamReader(copyShadowStream);
//            var responseBody = await reader.ReadToEndAsync();

//            // Write response back to App Insights
//            var requestToTelemetry = context.Features.Get<RequestTelemetry>();
//            requestToTelemetry?.Properties.Add("ResponseBody", responseBody);
//        }
//        finally
//        {
//            context.Response.Body = originalBodyStream;
//        }
//    }
//}
