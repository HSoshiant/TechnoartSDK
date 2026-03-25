using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
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
        string terminationText = "Approved",
        JsonElement? responseSchema = null) where T : class
    {
        logger.LogInformation("Generating writer review for {OperationName} with \"{WriterService}\" as writer and \"{ReviewerService}\" as reviewer",
            operationName, writerServiceName, reviewerServiceName);

        var writerAgent = new ChatCompletionAgent
        {
            Name = "Writer",
            Instructions = writerInstructions,
            Kernel = kernel.Clone(),
            Arguments = new KernelArguments(BuildWriterSettings(writerServiceName, typeof(T), responseSchema))
        };

        var reviewerAgent = new ChatCompletionAgent
        {
            Name = "Reviewer",
            Instructions = reviewerInstructions + $"\nProvide at least one review and then if the writer's response is satisfactory, return the text \"[{terminationText}]\" and nothing else",
            Kernel = kernel.Clone(),
            Arguments = new KernelArguments(BuildReviewerSettings(reviewerServiceName))
        };

        var initialMessages = BuildInitialMessages(chatHistory, inputText);
        var extraMessages = new List<ChatMessageContent>();

        while (true)
        {
            var chat = new AgentGroupChat(writerAgent, reviewerAgent)
            {
                ExecutionSettings = new AgentGroupChatSettings
                {
                    SelectionStrategy = new SequentialSelectionStrategy(),
                    TerminationStrategy = new ApprovalTerminationStrategy(terminationText)
                    {
                        // maxRound=0: writer only (1 turn); maxRound=N: writer→reviewer N times + 1 final writer turn
                        MaximumIterations = maxRound == 0 ? 1 : maxRound * 2 + 1,
                        Agents = [reviewerAgent]
                    }
                }
            };
            chat.AddChatMessages([.. initialMessages, .. extraMessages]);

            string lastWriterOutput = string.Empty;
            await foreach (var message in chat.InvokeAsync())
            {
                logger.LogInformation("{OperationName}: Agent '{Agent}' responded", operationName, message.AuthorName);
                if (message.AuthorName == writerAgent.Name)
                    lastWriterOutput = message.Content ?? string.Empty;
            }

            var response = typeof(T) == typeof(string)
                ? lastWriterOutput as T
                : JsonSerializer.Deserialize<T>(lastWriterOutput);

            var errors = checkErrors?.Invoke(response!);
            if ((errors?.Count ?? 0) == 0)
            {
                logger.LogInformation("{OperationName}: Completed successfully", operationName);
                return response!;
            }

            var errorsStr = string.Join("\n", errors!);
            logger.LogInformation("{OperationName}: invalid result: {Errors}", operationName, errorsStr);
            extraMessages.Add(new ChatMessageContent(AuthorRole.User, errorsStr));
        }
    }

    #endregion Methods

    #region Fields
    #endregion Fields

    #region Static Fields
    #endregion Static Fields

    #region Static Methods
    private static List<ChatMessageContent> BuildInitialMessages(ChatHistory? chatHistory, string? inputText)
    {
        var messages = new List<ChatMessageContent>();
        if (chatHistory != null)
            messages.AddRange(chatHistory);
        if (!string.IsNullOrWhiteSpace(inputText))
            messages.Add(new ChatMessageContent(AuthorRole.User, inputText));
        return messages;
    }

    private static PromptExecutionSettings BuildWriterSettings(string serviceName, Type responseType, JsonElement? responseSchema) =>
        serviceName switch
        {
            AIServicesExtensions.GoogleAIServicePro or AIServicesExtensions.GoogleAIService =>
                new GeminiPromptExecutionSettings
                {
                    ServiceId = serviceName,
                    Temperature = 0.8,
                    MaxTokens = int.MaxValue,
                    ResponseMimeType = responseType == typeof(string) ? "text/plain" : "application/json",
                    ResponseSchema = responseType == typeof(string) ? null : responseType,
                    ThinkingConfig = new GeminiThinkingConfig { IncludeThoughts = false, ThinkingLevel = "low" }
                },
            AIServicesExtensions.OpenAIServiceMini or AIServicesExtensions.OpenAIService =>
                new OpenAIPromptExecutionSettings
                {
                    ServiceId = serviceName,
                    Temperature = 1,
                    MaxTokens = 32768,
                    ResponseFormat = responseSchema is not null ? responseSchema : responseType == typeof(string) ? null : responseType,
                    ReasoningEffort = "medium"
                },
            _ => throw new ArgumentException($"Unsupported service name: {serviceName}", nameof(serviceName))
        };

    private static PromptExecutionSettings BuildReviewerSettings(string serviceName) =>
        serviceName switch
        {
            AIServicesExtensions.GoogleAIServicePro or AIServicesExtensions.GoogleAIService =>
                new GeminiPromptExecutionSettings { ServiceId = serviceName, Temperature = 0.8, MaxTokens = 32768 },
            AIServicesExtensions.OpenAIServiceMini or AIServicesExtensions.OpenAIService =>
                new OpenAIPromptExecutionSettings { ServiceId = serviceName, Temperature = 0.8f, MaxTokens = 32768 },
            _ => throw new ArgumentException($"Unsupported service name: {serviceName}", nameof(serviceName))
        };
    #endregion Static Methods
}

internal sealed class ApprovalTerminationStrategy(string terminationText) : TerminationStrategy
{
    protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
    {
        var lastMessage = history[^1];
        return Task.FromResult(
            lastMessage.Content?.Trim().Contains(terminationText, StringComparison.OrdinalIgnoreCase) == true
        );
    }
}

#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.