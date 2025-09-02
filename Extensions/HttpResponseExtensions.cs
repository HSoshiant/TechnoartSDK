using Newtonsoft.Json;

namespace TechnoartSDK.Extensions;

public static class HttpResponseExtensions
{
    public static async IAsyncEnumerable<T> ReadAsyncEnum<T>(this HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                var location = JsonConvert.DeserializeObject<T>(line);
                if (location != null)
                    yield return location;
            }
        }
        reader.Close();
        yield break;

    }
}
