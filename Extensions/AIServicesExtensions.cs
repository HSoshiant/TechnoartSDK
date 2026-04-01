using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TechnoartSDK.Extensions;

public static class AIServicesExtensions
{
    #region Static Fields

    public const string OpenAIService = nameof(OpenAIService);
    public const string OpenAIServiceMini = nameof(OpenAIServiceMini);
    public const string GoogleAIService = nameof(GoogleAIService);
    public const string GoogleAIServicePro = nameof(GoogleAIServicePro);        
    public const string OpenApiKey = "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";
    public const string GoogleApiKey = "AIzaSyAmMPi7ZpBLACqc2MGzuFr2YCOQXVEzRb8";

    #endregion Static Fields

    #region Static Methods

    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
#pragma warning disable SKEXP0070
        services.AddKeyedSingleton<IChatCompletionService>(OpenAIService, (sp, _) =>
            new OpenAIChatCompletionService(
                //"gpt-4.1-mini-2025-04-14",
                "gpt-5.2",
                OpenApiKey,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService")));

        services.AddKeyedSingleton<IChatCompletionService>(OpenAIServiceMini, (sp, _) =>
            new OpenAIChatCompletionService(
                //"gpt-4.1-mini-2025-04-14",
                "gpt-5-mini",
                OpenApiKey,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService")));

        services.AddKeyedSingleton<IChatCompletionService>(GoogleAIService, (sp, _) =>
            new GoogleAIGeminiChatCompletionService(
                "gemini-3-flash-preview",
                apiKey: GoogleApiKey,
                apiVersion: GoogleAIVersion.V1_Beta,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService")));

        services.AddKeyedSingleton<IChatCompletionService>(GoogleAIServicePro, (sp, _) =>
            new GoogleAIGeminiChatCompletionService(
                "gemini-3-pro-preview",
                apiKey: GoogleApiKey,
                apiVersion: GoogleAIVersion.V1_Beta,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService")));

        //services.AddSingleton<ImageClient>(sp=>new()
        services
            .AddSingleton(sp => new Kernel(sp));

        //services.AddSingleton<IGeminiClient>(sp => new Common.GoogleGeminiClient(
        //    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService"), googleApiKey));
        //services.AddSingleton<OpenAIClient>(sp => new(openApiKey));

        return services;
    }

    #endregion Static Methods
}
