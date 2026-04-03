using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TechnoartSDK.Extensions;
using TechnoartSDK.Models;

namespace TechnoartSDK.AI;
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class WriterReviewerAIAgents(Kernel kernel, ILogger logger)
{
    #region Methods
    /// <summary>
    /// Runs a writer-reviewer agent exchange and returns the writer's final output deserialized as <typeparamref name="T"/>.
    /// Reports progress through <paramref name="progress"/> when provided.
    /// </summary>
    public async Task<T> GenerateWriterReviewAsync<T>(
        string operationName,
        string writerInstructions, string writerServiceName,
        string reviewerInstructions, string reviewerServiceName,
        string? inputText = null, int maxRound = 1,
        ChatHistory? chatHistory = null,
        Func<T, List<string>> checkErrors = null!,
        string terminationText = "Approved",
        JsonElement? responseSchema = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default) where T : class
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

        string lastWriterOutput = string.Empty;
        string? currentAuthor = null;
        var buffer = new StringBuilder();

        // Flushes accumulated content for the current agent
        void FlushAgent()
        {
            if (currentAuthor is null)
            {
                return;
            }

            var content = buffer.ToString();
            logger.LogInformation("{OperationName}: Agent '{Agent}' responded", operationName, currentAuthor);
            if (currentAuthor == writerAgent.Name)
            {
                lastWriterOutput = content;
            }

            progress?.Report(new OperationProgress
            {
                Type = OperationProgressType.StepCompleted,
                Subject = currentAuthor,
                Content = content
            });
        }

        var initialMessages = BuildInitialMessages(chatHistory, inputText);
        var extraMessages = new List<ChatMessageContent>();

        progress?.Report(new OperationProgress
        {
            Type = OperationProgressType.Started,
            Subject = operationName
        });

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

            // Reset per-round state
            lastWriterOutput = string.Empty;
            currentAuthor = null;
            buffer.Clear();

            // Stream token-by-token from the agent group chat
            await foreach (var chunk in chat.InvokeStreamingAsync(ct))
            {
                // Agent switch — flush previous, signal new
                if (chunk.AuthorName != currentAuthor)
                {
                    FlushAgent();
                    currentAuthor = chunk.AuthorName;
                    buffer.Clear();
                    progress?.Report(new OperationProgress
                    {
                        Type = OperationProgressType.StepStarted,
                        Subject = currentAuthor ?? operationName
                    });
                }

                if (chunk.Content is { } text)
                {
                    buffer.Append(text);
                    progress?.Report(new OperationProgress
                    {
                        Type = OperationProgressType.Info,
                        Subject = currentAuthor ?? operationName,
                        Content = text
                    });
                }
            }

            FlushAgent();

            var response = typeof(T) == typeof(string)
                ? lastWriterOutput as T
                : JsonSerializer.Deserialize<T>(lastWriterOutput);

            var errors = checkErrors?.Invoke(response!);
            if ((errors?.Count ?? 0) == 0)
            {
                logger.LogInformation("{OperationName}: Completed successfully", operationName);
                progress?.Report(new OperationProgress
                {
                    Type = OperationProgressType.Completed,
                    Subject = operationName
                });
                return response!;
            }

            var errorsStr = string.Join("\n", errors!);
            logger.LogInformation("{OperationName}: invalid result: {Errors}", operationName, errorsStr);
            ct.ThrowIfCancellationRequested();
            progress?.Report(new OperationProgress
            {
                Type = OperationProgressType.Warning,
                Subject = operationName,
                Content = errorsStr
            });
            extraMessages.Add(new ChatMessageContent(AuthorRole.User, errorsStr));
        }
    }

    #endregion Methods

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