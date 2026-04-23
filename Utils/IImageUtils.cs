namespace TechnoartSDK.Utils;

/// <summary>
/// Interface for image utility operations (resizing, thumbnails).
/// </summary>
public interface IImageUtils
{
    byte[] Resize(byte[] imageBytes, int maxWidth = 300, int maxHeight = 300, double ratio = 0);
}
