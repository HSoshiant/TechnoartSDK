namespace TechnoartSDK.Models;

/// <summary>Requested models and provider-reported identity and usage for one image-generation request.</summary>
public sealed record ImageGenerationMetadata
{
    /// <summary>Provider receiving the request.</summary>
    public required string Provider { get; init; }

    /// <summary>API transport used for the request.</summary>
    public required string Transport { get; init; }

    /// <summary>Provider HTTP request identifier, when returned.</summary>
    public string? RequestId { get; init; }

    /// <summary>Provider response identifier, when returned.</summary>
    public string? ResponseId { get; init; }

    /// <summary>Identifier of the sole image-generation tool call, when returned.</summary>
    public string? ImageCallId { get; init; }

    /// <summary>Explicitly requested mainline model, billed separately from image generation.</summary>
    public string? RequestedDispatcherModel { get; init; }

    /// <summary>Mainline model reported by the response, not inferred from the request.</summary>
    public string? ActualDispatcherModel { get; init; }

    /// <summary>Explicitly requested image-generation model.</summary>
    public string? RequestedImageModel { get; init; }

    /// <summary>Image model separately reported by the tool call; an echoed tool definition is not evidence.</summary>
    public string? ActualImageModel { get; init; }

    /// <summary>Usage reported for the mainline response, separate from image-tool usage.</summary>
    public ImageGenerationTokenUsage? DispatcherUsage { get; init; }

    /// <summary>Usage separately reported by the image tool, or null when it is not exposed.</summary>
    public ImageGenerationTokenUsage? ImageUsage { get; init; }
}
