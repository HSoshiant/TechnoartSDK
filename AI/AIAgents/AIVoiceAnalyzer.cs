using Newtonsoft.Json;
using OpenAI.Audio;
using TechnoartSDK.Extensions;

namespace TechnoartSDK.AI.AIAgents;

public class AIVoiceAnalyzer
{
    public static async Task<AudioTranscription> Transcribe(string audioPath)
    {
        AudioClient client = new(
            model: "whisper-1",
            apiKey: AIServicesExtensions.OpenApiKey
        );
        AudioTranscription transcription = await client.TranscribeAudioAsync(audioPath, new()
        {
            //Includes = AudioTranscriptionIncludes.Default,
            TimestampGranularities = AudioTimestampGranularities.Word,
            ResponseFormat = AudioTranscriptionFormat.Verbose

        });
        var json = JsonConvert.SerializeObject(transcription.Words);
        return transcription;
    }
}
