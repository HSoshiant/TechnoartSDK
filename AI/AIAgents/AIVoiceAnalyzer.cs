using Microsoft.Extensions.Options;
using OpenAI.Audio;
using TechnoartSDK.Models;

namespace TechnoartSDK.AI.AIAgents;

/// <summary>
/// Transcribes audio using OpenAI Whisper, reading keys and model from <see cref="AIServicesConfig"/>.
/// </summary>
public class AIVoiceAnalyzer(IOptions<AIServicesConfig> aiServicesConfig)
{
    #region Methods

    /// <summary>
    /// Transcribes the audio file at the given path using the configured Whisper model.
    /// </summary>
    public async Task<AudioTranscription> Transcribe(string audioPath)
    {
        var cfg = aiServicesConfig.Value;
        AudioClient client = new(
            model: cfg.WhisperModel,
            apiKey: cfg.OpenAIApiKey
        );
        AudioTranscription transcription = await client.TranscribeAudioAsync(audioPath, new()
        {
            TimestampGranularities = AudioTimestampGranularities.Word,
            ResponseFormat = AudioTranscriptionFormat.Verbose
        });
        return transcription;
    }

    #endregion Methods
}
