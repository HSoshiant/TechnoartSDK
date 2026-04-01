namespace TechnoartSDK.Models;

public interface IFileModel
{
    public void ReadFrom(Stream stream);
    public void WriteTo(Stream stream); 
}
