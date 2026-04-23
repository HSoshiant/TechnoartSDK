using Newtonsoft.Json;

namespace TechnoartSDK.Models;

/// <summary>
/// Standard error response body returned by API controllers on 4xx/5xx responses.
/// </summary>
public class ApiErrorResponse
{
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}
