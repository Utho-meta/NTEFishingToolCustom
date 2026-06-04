namespace NTEFishingToolCustom.Imaging;

using System.Drawing;
using NTEFishingToolCustom.Native;

public static class WindowCapture
{
    public static Bitmap? Capture(IntPtr hWnd)
    {
        if (!WindowApi.GetPureClientRect(hWnd, out var rect))
        {
            return null;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        return bitmap;
    }
}
