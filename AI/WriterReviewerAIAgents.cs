using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TechnoartSDK.Extensions;

namespace TechnoartSDK.AI;
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class WriterReviewerAIAgents(Kernel kernel, ILogger logger)
{

    #region Constructors
    #endregion Constructors

    #region Methods
    public async Task<T> GenerateWriterReviewAsync<T>(
        string operationName,
        string writerInstructions, string writerServiceName,
        string reviewerInstructions, string reviewerServiceName, string reviewerPrompt,
        string? inputText = null, int maxRound = 1,
        ChatHistory? chatHistory = null,
        Func<T, List<string>> checkErrors = null!,
        string terminationText = "Approved") where T : class
    {
        logger.LogInformation($"Generating writer review for {operationName} with \"{writerServiceName}\" as writer and \"{reviewerServiceName}\" as reviewer ");

        var writerChatCompletionService = kernel.GetRequiredService<IChatCompletionService>(writerServiceName);
        var reviewerChatCompletionService = kernel.GetRequiredService<IChatCompletionService>(reviewerServiceName);

        var writerChatHistory = new ChatHistory();
        var reviewerChatHistory = new ChatHistory();

        writerChatHistory.AddSystemMessage(writerInstructions);
        reviewerChatHistory.AddSystemMessage(reviewerInstructions + @$"
Provide at least one review and then if the writer's response is satisfactory, return the text ""[{terminationText}]"" and nothing else");

        if (chatHistory != null)
        {
            writerChatHistory.AddRange(chatHistory);
            reviewerChatHistory.AddRange(chatHistory);
        }

        if (!string.IsNullOrWhiteSpace(inputText))
        {
            writerChatHistory.AddUserMessage(inputText);
            reviewerChatHistory.AddUserMessage(inputText);
        }

        PromptExecutionSettings writeExecutionSettings = writerServiceName switch
        {
            AIServicesExtensions.GoogleAIServicePro or
            AIServicesExtensions.GoogleAIService => new GeminiPromptExecutionSettings()
            {
                Temperature = 0.8,
                MaxTokens = int.MaxValue,
                ResponseMimeType = typeof(T) == typeof(string) ? "text/plain" : "application/json",
                ResponseSchema = typeof(T) == typeof(string) ? null : typeof(T),
                ThinkingConfig = new GeminiThinkingConfig()
                {
                    IncludeThoughts = false,
                    ThinkingLevel = "low"
                }
            },
            AIServicesExtensions.OpenAIServiceMini or
            AIServicesExtensions.OpenAIService => new OpenAIPromptExecutionSettings()
            {
                Temperature = 0.8f,
                MaxTokens = 32768,
                ResponseFormat = typeof(T) == typeof(string) ? null : typeof(T)
            },
            _ => throw new ArgumentException($"Unsupported service name: {writerServiceName}", nameof(writerServiceName)),
        };


        PromptExecutionSettings reviewerExecutionSettings = reviewerServiceName switch
        {
            AIServicesExtensions.GoogleAIServicePro or
            AIServicesExtensions.GoogleAIService => new GeminiPromptExecutionSettings()
            {
                Temperature = 0.8,
                MaxTokens = 32768,
            },
            AIServicesExtensions.OpenAIServiceMini or
            AIServicesExtensions.OpenAIService => new OpenAIPromptExecutionSettings()
            {
                Temperature = 0.8f,
                MaxTokens = 32768,

            },
            _ => throw new ArgumentException($"Unsupported service name: {reviewerServiceName}", nameof(reviewerServiceName)),
        };

        var clonedKernel = kernel.Clone();

        T? GetResponse(string lastEdition)
        {
            var respose = typeof(T) == typeof(string)
                   ? lastEdition as T
                   : JsonSerializer.Deserialize<T>(lastEdition)!;

            var errors = checkErrors?.Invoke(respose);
            if ((errors?.Count ?? 0) != 0)
            {
                var errorsStr = string.Join("\n", errors!);
                logger.LogInformation("{OperationName}: invalid result: {Errors}", errorsStr);
                writerChatHistory.AddUserMessage(errorsStr);
                return null;
            }
            return respose;
        }
        while (true)
        {
            logger.LogInformation("{OperationName}: Starting writer {Round} round(s) Left", operationName, maxRound + 1);
            var result = await writerChatCompletionService.GetChatMessageContentAsync(writerChatHistory, writeExecutionSettings, clonedKernel);
            var lastEdition = result.Content!;
            if (maxRound == 0)
            {
                var respose = GetResponse(lastEdition);
                if (respose == null)
                {
                    continue; // If the response is invalid, continue to the next round
                }
                logger.LogInformation("{OperationName}: Writer round completed result: {LastEdition}", operationName, lastEdition);
                return respose;
            }
            maxRound--;

            writerChatHistory.AddAssistantMessage(lastEdition);
            reviewerChatHistory.AddUserMessage($@"
{reviewerPrompt}
{lastEdition}
");

            logger.LogInformation("{OperationName}: Starting reviewer {Round} round(s) Left", operationName, maxRound + 1);
            result = await reviewerChatCompletionService.GetChatMessageContentAsync(reviewerChatHistory, reviewerExecutionSettings, clonedKernel);
            if (result.Content.Trim().Contains(terminationText, StringComparison.OrdinalIgnoreCase))
            {
                var respose = GetResponse(lastEdition);
                if (respose == null)
                {
                    continue; // If the response is invalid, continue to the next round
                }
                logger.LogInformation("{OperationName}: Review completed with termination text: {TerminationText}", operationName, terminationText);
                return respose;
            }

            writerChatHistory.AddUserMessage(result.Content);
            reviewerChatHistory.AddAssistantMessage(result.Content);
        }
    }

    #endregion Methods
    #region Fields
    #endregion Fields

    #region Static Fields
    #endregion Static Fields

    #region Static Methods
    #endregion Static Methods
}
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.