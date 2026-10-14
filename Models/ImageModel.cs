using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace TechnoartSDK.Models;

/// <summary>Image data and optional provider-reported generation metadata.</summary>
public record ImageModel : RepositoryModel, IFileModel
{
    public string Data { get; set; }
    public string MimeType { get; set; } = "image/png";

    /// <summary>Generation facts for JSON checkpoints, not an entity relationship; absent facts do not imply zero cost.</summary>
    [NotMapped, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageGenerationMetadata? GenerationMetadata { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        Data = Convert.ToBase64String(memoryStream.ToArray());
    }

    public void WriteTo(Stream stream)
    {
        var bytes = Convert.FromBase64String(Data);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
}
