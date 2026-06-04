namespace NTEFishingToolCustom.Imaging;

using System.Drawing;

public static class SimpleTemplateMatcher
{
    public static bool Exists(Bitmap source, Bitmap template, double minSimilarity = 0.82)
    {
        if (template.Width > source.Width || template.Height > source.Height)
        {
            return false;
        }

        var stepX = Math.Max(1, template.Width / 12);
        var stepY = Math.Max(1, template.Height / 12);
        var stride = Math.Max(6, Math.Min(template.Width, template.Height) / 3);

        for (var y = 0; y <= source.Height - template.Height; y += stride)
        {
            for (var x = 0; x <= source.Width - template.Width; x += stride)
            {
                if (SimilarityAt(source, template, x, y, stepX, stepY) >= minSimilarity)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double SimilarityAt(Bitmap source, Bitmap template, int offsetX, int offsetY, int stepX, int stepY)
    {
        var total = 0;
        var matched = 0;

        for (var y = 0; y < template.Height; y += stepY)
        {
            for (var x = 0; x < template.Width; x += stepX)
            {
                total++;
                var a = source.GetPixel(offsetX + x, offsetY + y);
                var b = template.GetPixel(x, y);
                var distance = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
                if (distance <= 72)
                {
                    matched++;
                }
            }
        }

        return total == 0 ? 0 : matched / (double)total;
    }
}
