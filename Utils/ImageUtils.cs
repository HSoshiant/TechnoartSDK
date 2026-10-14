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
    /// Produces a PNG at the exact positive pixel dimensions by scaling uniformly
    /// and cropping equally from opposite edges, without stretching or padding.
    /// </summary>
    public static byte[] ResizeExact(byte[] imageBytes, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        using var inputStream = new MemoryStream(imageBytes);
        using var originalImage = Image.FromStream(inputStream);

        // Crop in source coordinates so the destination is filled with one uniform scale.
        double scale = Math.Max((double)width / originalImage.Width, (double)height / originalImage.Height);
        float sourceWidth = (float)(width / scale);
        float sourceHeight = (float)(height / scale);
        float sourceX = (originalImage.Width - sourceWidth) / 2;
        float sourceY = (originalImage.Height - sourceHeight) / 2;

        using var resizedImage = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(resizedImage);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Mirror edge samples to avoid translucent borders from bicubic interpolation.
        using var imageAttributes = new ImageAttributes();
        imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(originalImage, new Rectangle(0, 0, width, height),
            sourceX, sourceY, sourceWidth, sourceHeight, GraphicsUnit.Pixel, imageAttributes);

        using var outputStream = new MemoryStream();
        resizedImage.Save(outputStream, ImageFormat.Png);
        return outputStream.ToArray();
    }

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
