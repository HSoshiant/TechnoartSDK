namespace TechnoartSDK.Models;

public interface IFileModel
{
    public void ReadFrom(Stream stream);
    public void WriteTo(Stream stream); 
}

public record FileModel(string Name, Stream Data);
