using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace TechnoartSDK.AI.AIAgents;

/// <summary>Lets the standard history reducer use a caller's validated, tool-free summary request.</summary>
internal sealed class ContextSummaryService(
    Func<ChatHistory, CancellationToken, Task<string>> summarize, ILogger logger) : IChatCompletionService
{
    #region Methods

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
        CancellationToken cancellationToken = default) =>
        [new(AuthorRole.Assistant, await summarize(chatHistory, cancellationToken))];

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogError("Context summaries must complete before replacing working history");
        throw new NotSupportedException("Context summaries are non-streaming.");
    }

    #endregion Methods

    #region Fields

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    #endregion Fields
}
