namespace TechnoartSDK.Models;

/// <summary>Reported token counts for one model. Missing or unobservable counts remain null, never estimated.</summary>
public sealed record ImageGenerationTokenUsage
{
    /// <summary>Total input tokens reported by this model.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Reported cached input tokens. Image-tool cache hits are not observable and remain null.</summary>
    public long? CachedInputTokens { get; init; }

    /// <summary>Reported cache-write input tokens, when observable for the dispatcher.</summary>
    public long? CacheWriteInputTokens { get; init; }

    /// <summary>Reported text component of the input tokens.</summary>
    public long? TextInputTokens { get; init; }

    /// <summary>Reported image component of the input tokens.</summary>
    public long? ImageInputTokens { get; init; }

    /// <summary>Total output tokens reported by this model.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Reported text component of the output tokens.</summary>
    public long? TextOutputTokens { get; init; }

    /// <summary>Reported image component of the output tokens.</summary>
    public long? ImageOutputTokens { get; init; }

    /// <summary>Reported reasoning component of the dispatcher's output tokens.</summary>
    public long? ReasoningOutputTokens { get; init; }

    /// <summary>Total tokens reported by this model; not calculated when the provider omits it.</summary>
    public long? TotalTokens { get; init; }
}
