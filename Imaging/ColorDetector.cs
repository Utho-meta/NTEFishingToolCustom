namespace NTEFishingToolCustom.Imaging;

using System.Drawing;

public readonly record struct RgbRange(Color Target, int Tolerance)
{
    public static readonly RgbRange FishBar = new(Color.FromArgb(49, 218, 183), 42);
    public static readonly RgbRange FishPointer = new(Color.FromArgb(245, 246, 159), 48);
    public static readonly RgbRange WhiteText = new(Color.White, 36);
}

public sealed record AreaDetection(Rectangle Bounds)
{
    public Point Center => new(Bounds.Left + Bounds.Width / 2, Bounds.Top + Bounds.Height / 2);
}

public static class ColorDetector
{
    public static Point? FindLargestAreaCenter(Bitmap bitmap, Rectangle rect, RgbRange range)
    {
        return FindLargestArea(bitmap, rect, range)?.Center;
    }

    public static AreaDetection? FindLargestArea(Bitmap bitmap, Rectangle rect, RgbRange range)
    {
        rect.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var count = 0;

        for (var y = rect.Top; y < rect.Bottom; y += 2)
        {
            for (var x = rect.Left; x < rect.Right; x += 2)
            {
                if (!IsClose(bitmap.GetPixel(x, y), range.Target, range.Tolerance))
                {
                    continue;
                }

                count++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (count < 6)
        {
            return null;
        }

        return new AreaDetection(Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1));
    }

    private static bool IsClose(Color pixel, Color target, int tolerance)
    {
        return Math.Abs(pixel.R - target.R)
            + Math.Abs(pixel.G - target.G)
            + Math.Abs(pixel.B - target.B) <= tolerance;
    }
}
