using Newtonsoft.Json;

namespace TechnoartSDK.Models;

public record RepositoryModel
{
    [JsonProperty("id")]
    public string Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
