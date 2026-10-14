using TechnoartSDK.Models;

namespace TechnoartSDK.AI.AIAgents;

/// <summary>An image request failed after submission, with any available identity and usage retained.</summary>
public sealed class ImageGenerationException : InvalidOperationException
{
    #region Methods

    /// <summary>Creates an image-generation failure without implying that the request is safe to replay.</summary>
    public ImageGenerationException(string message, ImageGenerationMetadata? generationMetadata, Exception? innerException = null)
        : base(message, innerException)
    {
        GenerationMetadata = generationMetadata;
    }

    #endregion Methods

    #region Fields

    /// <summary>Known generation facts, which may be incomplete even when paid work occurred.</summary>
    public ImageGenerationMetadata? GenerationMetadata { get; }

    #endregion Fields
}
