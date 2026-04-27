using System.ComponentModel.DataAnnotations;

namespace TechnoartSDK.Models;

/// <summary>
/// Configuration options for AI services (OpenAI, Google AI).
/// Bound from appsettings.json section "AIServices".
/// </summary>
public class AIServicesConfig
{
    public const string SectionName = "AIServices";

    /// <summary>OpenAI API key for chat, image, and audio services.</summary>
    [Required]
    public required string OpenAIApiKey { get; set; }

    /// <summary>Google AI API key for Gemini and Imagen services.</summary>
    [Required]
    public required string GoogleApiKey { get; set; }

    /// <summary>OpenAI primary chat model name.</summary>
    [Required]
    public required string OpenAIModel { get; set; }

    /// <summary>OpenAI lightweight chat model name.</summary>
    [Required]
    public required string OpenAIMiniModel { get; set; }

    /// <summary>Google AI flash-tier chat model name.</summary>
    [Required]
    public required string GoogleAIModel { get; set; }

    /// <summary>Google AI pro-tier chat model name.</summary>
    [Required]
    public required string GoogleAIProModel { get; set; }

    /// <summary>OpenAI Whisper model name for audio transcription.</summary>
    [Required]
    public required string WhisperModel { get; set; }

    /// <summary>Google Imagen model name for image generation.</summary>
    [Required]
    public required string ImagenModel { get; set; }
}
