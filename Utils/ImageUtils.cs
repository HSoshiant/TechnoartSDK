using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TechnoartSDK.Utils;

/// <summary>
/// Image manipulation utilities (resize, thumbnail generation).
/// </summary>
public static class ImageUtils
{
    /// <summary>
    /// Resizes raw image bytes, preserving aspect ratio.
    /// When <paramref name="ratio"/> is non-zero it scales by that factor;
    /// otherwise it fits within <paramref name="maxWidth"/> × <paramref name="maxHeight"/>.
    /// Returns the original bytes unchanged when already within bounds and no ratio is specified.
    /// </summary>
    public static byte[] Resize(byte[] imageBytes, int maxWidth = 300, int maxHeight = 300, double ratio = 0)
    {
        using var inputStream = new MemoryStream(imageBytes);
        using var originalImage = Image.FromStream(inputStream);

        int originalWidth = originalImage.Width;
        int originalHeight = originalImage.Height;

        if (ratio == 0)
        {
            if (originalWidth <= maxWidth && originalHeight <= maxHeight)
            {
                return imageBytes;
            }

            double ratioX = (double)maxWidth / originalWidth;
            double ratioY = (double)maxHeight / originalHeight;
            ratio = Math.Min(ratioX, ratioY);
        }

        int newWidth = (int)(originalWidth * ratio);
        int newHeight = (int)(originalHeight * ratio);

        using var thumbnail = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(thumbnail);

        graphics.Clear(Color.White);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);

        using var outputStream = new MemoryStream();
        thumbnail.Save(outputStream, ImageFormat.Png);
        return outputStream.ToArray();
    }
}
