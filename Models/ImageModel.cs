namespace TechnoartSDK.Models;

public record ImageModel : RepositoryModel, IFileModel
{
    public string Data { get; set; }
    public string MimeType { get; set; } = "image/png";

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
