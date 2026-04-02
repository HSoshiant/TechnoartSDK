using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TechnoartSDK.Extensions;

namespace TechnoartSDK.AI.AIAgents;
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class AIChatCompletion(Kernel kernel, ILogger logger)
{
    public async Task<T> Complete<T>(string operationName, string serviceName, ChatHistory? chatHistory, int? maxToken = null, CancellationToken ct = default) where T : class
    {
        logger.LogInformation("{OperationName}: Starting AI chat completion with service '{ServiceName}'", operationName, serviceName);
        if (chatHistory == null)
        {
            throw new ArgumentException("ChatHistory cannot be null.", nameof(chatHistory));
        }

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>(serviceName);

        PromptExecutionSettings writeExecutionSettings = serviceName switch
        {
            AIServicesExtensions.GoogleAIService => new GeminiPromptExecutionSettings()
            {
                Temperature = 0.8,
                MaxTokens = maxToken ?? int.MaxValue,
                ResponseMimeType = typeof(T) == typeof(string) ? "text/plain" : "application/json",
                ResponseSchema = typeof(T) == typeof(string) ? null : typeof(T),
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            },
            AIServicesExtensions.OpenAIServiceMini or
            AIServicesExtensions.OpenAIService => new OpenAIPromptExecutionSettings()
            {
                Temperature = 1f,
                MaxTokens = maxToken ?? 32768,
                ResponseFormat = typeof(T) == typeof(string) ? null : typeof(T),
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            },
            _ => throw new ArgumentException($"Unsupported service name: {serviceName}", nameof(serviceName)),
        };

        var localChatHistory = new ChatHistory(chatHistory);

        var result = await chatCompletionService.GetChatMessageContentAsync(localChatHistory, writeExecutionSettings, kernel, ct);

        var content = result.Content!;
        var respose = typeof(T) == typeof(string)
           ? content as T
           : JsonSerializer.Deserialize<T>(content)!;

        return respose;
    }
}

#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

