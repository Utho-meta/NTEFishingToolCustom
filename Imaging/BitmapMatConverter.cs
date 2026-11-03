namespace NTEFishingToolCustom.Imaging;

using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;

public static class BitmapMatConverter
{
    public static Mat ToBgraMat(Bitmap bitmap)
    {
        if (bitmap.PixelFormat is PixelFormat.Format32bppArgb or PixelFormat.Format32bppPArgb or PixelFormat.Format32bppRgb)
        {
            return LockAndClone(bitmap, bitmap.PixelFormat);
        }

        using var normalized = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalized))
        {
            graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }

        return LockAndClone(normalized, PixelFormat.Format32bppArgb);
    }

    private static Mat LockAndClone(Bitmap bitmap, PixelFormat pixelFormat)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, pixelFormat);
        try
        {
            using var wrapped = Mat.FromPixelData(bitmap.Height, bitmap.Width, MatType.CV_8UC4, data.Scan0, data.Stride);
            return wrapped.Clone();
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
