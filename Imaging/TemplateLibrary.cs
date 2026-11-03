namespace NTEFishingToolCustom.Imaging;

using System.Drawing;
using OpenCvSharp;
using Point = System.Drawing.Point;

public sealed class TemplateLibrary
{
    private readonly Dictionary<string, Bitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Mat> _matCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _templateDir;
    private readonly Action<string> _log;
    private bool _reportedMissing;

    public TemplateLibrary(string baseDirectory, Action<string> log)
    {
        _templateDir = Path.Combine(baseDirectory, "Resources", "Images");
        _log = log;
        LoadAll();
    }

    public bool Exists(string name, Bitmap source)
    {
        return Exists(name, source, null);
    }

    public bool Exists(string name, Bitmap source, Rectangle? searchRegion, double minSimilarity = 0.72)
    {
        return FindCenter(name, source, searchRegion, minSimilarity) is not null;
    }

    public Point? FindCenter(string name, Bitmap source, Rectangle? searchRegion, double minSimilarity = 0.72)
    {
        if (!_matCache.TryGetValue(name, out var template))
        {
            return null;
        }

        var region = searchRegion ?? new Rectangle(Point.Empty, source.Size);
        region.Intersect(new Rectangle(Point.Empty, source.Size));
        if (region.Width <= 0 || region.Height <= 0)
        {
            return null;
        }

        using var sourceMat = BitmapMatConverter.ToBgraMat(source);
        using var crop = new Mat(sourceMat, new Rect(region.X, region.Y, region.Width, region.Height));
        using var matchSource = ToMatchMat(crop);
        var loc = FindImageCenter(matchSource, template, minSimilarity);
        if (loc is null)
        {
            return null;
        }

        return new Point(region.X + loc.Value.X, region.Y + loc.Value.Y);
    }

    public Point? FindCenterScaled(
        string name,
        Bitmap source,
        Rectangle? searchRegion,
        double minSimilarity = 0.72,
        double[]? scales = null)
    {
        var match = FindBestScaled(name, source, searchRegion, scales);
        if (match is null || match.Value.Similarity < minSimilarity)
        {
            return null;
        }

        return match.Value.Center;
    }

    public (Point Center, double Similarity, double Scale)? FindBestScaled(
        string name,
        Bitmap source,
        Rectangle? searchRegion,
        double[]? scales = null)
    {
        if (!_matCache.TryGetValue(name, out var template))
        {
            return null;
        }

        var region = searchRegion ?? new Rectangle(Point.Empty, source.Size);
        region.Intersect(new Rectangle(Point.Empty, source.Size));
        if (region.Width <= 0 || region.Height <= 0)
        {
            return null;
        }

        using var sourceMat = BitmapMatConverter.ToBgraMat(source);
        using var crop = new Mat(sourceMat, new Rect(region.X, region.Y, region.Width, region.Height));
        using var matchSource = ToMatchMat(crop);
        var loc = FindImageCenterScaledBest(
            matchSource,
            template,
            scales ?? new[] { 1.6, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8, 3.0, 3.2 });
        if (loc is null)
        {
            return null;
        }

        return (
            new Point(region.X + loc.Value.Center.X, region.Y + loc.Value.Center.Y),
            loc.Value.Similarity,
            loc.Value.Scale);
    }

    private static Point? FindImageCenter(Mat source, Mat template, double minSimilarity)
    {
        if (template.Width > source.Width || template.Height > source.Height)
        {
            return null;
        }

        using var result = new Mat();
        Cv2.MatchTemplate(source, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
        if (maxVal < minSimilarity)
        {
            return null;
        }

        return new Point(maxLoc.X + template.Width / 2, maxLoc.Y + template.Height / 2);
    }

    private static (Point Center, double Similarity, double Scale)? FindImageCenterScaledBest(Mat source, Mat template, IReadOnlyList<double> scales)
    {
        var bestValue = double.MinValue;
        var bestScale = 0d;
        OpenCvSharp.Point bestLocation = default;
        OpenCvSharp.Size bestSize = default;

        foreach (var scale in scales)
        {
            var scaledWidth = Math.Max(1, (int)Math.Round(template.Width * scale));
            var scaledHeight = Math.Max(1, (int)Math.Round(template.Height * scale));
            if (scaledWidth > source.Width || scaledHeight > source.Height)
            {
                continue;
            }

            using var scaledTemplate = new Mat();
            Cv2.Resize(template, scaledTemplate, new OpenCvSharp.Size(scaledWidth, scaledHeight), 0, 0, InterpolationFlags.Linear);
            using var result = new Mat();
            Cv2.MatchTemplate(source, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
            if (maxVal > bestValue)
            {
                bestValue = maxVal;
                bestScale = scale;
                bestLocation = maxLoc;
                bestSize = scaledTemplate.Size();
            }
        }

        if (bestSize.Width <= 0 || bestSize.Height <= 0)
        {
            return null;
        }

        return (
            new Point(bestLocation.X + bestSize.Width / 2, bestLocation.Y + bestSize.Height / 2),
            bestValue,
            bestScale);
    }

    private void LoadAll()
    {
        if (!Directory.Exists(_templateDir))
        {
            Directory.CreateDirectory(_templateDir);
        }

        foreach (var path in Directory.GetFiles(_templateDir, "*.png"))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var logicalName = NormalizeName(fileName);
            using var stream = File.OpenRead(path);
            var bitmap = new Bitmap(stream);
            _bitmapCache[logicalName] = bitmap;
            using var rawMat = BitmapMatConverter.ToBgraMat(bitmap);
            _matCache[logicalName] = ToMatchMat(rawMat);
        }

        if (_bitmapCache.Count == 0 && !_reportedMissing)
        {
            _reportedMissing = true;
            _log("Resources/Images has no templates; color detection only.");
        }
    }

    private static string NormalizeName(string fileName)
    {
        if (fileName.StartsWith("HTGame-", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName["HTGame-".Length..];
        }

        var lastDash = fileName.LastIndexOf('-');
        if (lastDash > 0 && int.TryParse(fileName[(lastDash + 1)..], out _))
        {
            fileName = fileName[..lastDash];
        }

        return fileName;
    }

    private static Mat ToMatchMat(Mat image)
    {
        var gray = new Mat();
        if (image.Channels() == 1)
        {
            image.CopyTo(gray);
            return gray;
        }

        Cv2.CvtColor(
            image,
            gray,
            image.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
