using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TechnoartSDK.Models;

namespace TechnoartSDK.Extensions;

/// <summary>
/// Registers Semantic Kernel AI chat completion services using <see cref="AIServicesConfig"/>.
/// </summary>
public static class AIServicesExtensions
{
    #region Static Fields

    public const string OpenAIService = nameof(OpenAIService);
    public const string OpenAIServiceMini = nameof(OpenAIServiceMini);
    public const string GoogleAIService = nameof(GoogleAIService);
    public const string GoogleAIServicePro = nameof(GoogleAIServicePro);

    #endregion Static Fields

    #region Static Methods

    /// <summary>
    /// Registers keyed <see cref="IChatCompletionService"/> instances and a <see cref="Kernel"/> singleton.
    /// Reads model names and API keys from <see cref="IOptions{AIServicesConfig}"/>.
    /// </summary>
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
#pragma warning disable SKEXP0070 // Google AI connector is experimental
        services.AddKeyedSingleton<IChatCompletionService>(OpenAIService, (sp, _) =>
        {
            var cfg = sp.GetRequiredService<IOptions<AIServicesConfig>>().Value;
            return new OpenAIChatCompletionService(
                cfg.OpenAIModel,
                cfg.OpenAIApiKey,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService"));
        });

        services.AddKeyedSingleton<IChatCompletionService>(OpenAIServiceMini, (sp, _) =>
        {
            var cfg = sp.GetRequiredService<IOptions<AIServicesConfig>>().Value;
            return new OpenAIChatCompletionService(
                cfg.OpenAIMiniModel,
                cfg.OpenAIApiKey,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService"));
        });

        services.AddKeyedSingleton<IChatCompletionService>(GoogleAIService, (sp, _) =>
        {
            var cfg = sp.GetRequiredService<IOptions<AIServicesConfig>>().Value;
            return new GoogleAIGeminiChatCompletionService(
                cfg.GoogleAIModel,
                apiKey: cfg.GoogleApiKey,
                apiVersion: GoogleAIVersion.V1_Beta,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService"));
        });

        services.AddKeyedSingleton<IChatCompletionService>(GoogleAIServicePro, (sp, _) =>
        {
            var cfg = sp.GetRequiredService<IOptions<AIServicesConfig>>().Value;
            return new GoogleAIGeminiChatCompletionService(
                cfg.GoogleAIProModel,
                apiKey: cfg.GoogleApiKey,
                apiVersion: GoogleAIVersion.V1_Beta,
                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChatCompletionService"));
        });
#pragma warning restore SKEXP0070

        services.AddSingleton(sp => new Kernel(sp));

        return services;
    }

    #endregion Static Methods
}
