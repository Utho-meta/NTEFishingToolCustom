namespace NTEFishingToolCustom.Imaging;

using System.Drawing;
using NTEFishingToolCustom.Automation;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

public static class FrameAnalyzer
{
    private static readonly Scalar FishBarRgb = new(49, 218, 183);
    private static readonly double[] BaitEmptyTipScales = { 0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.00, 1.05, 1.10, 1.20, 1.30 };

    public static FrameFacts Analyze(Bitmap frame, IntPtr window, TemplateLibrary templates, bool preferFightFastPath = false)
    {
        var meterRect = FishingMeterRect(frame);
        var centerTipRect = RelativeRect(frame, 0.28, 0.36, 0.44, 0.20);
        var startButtonRect = RelativeRect(frame, 0.56, 0.74, 0.42, 0.20);
        var baitTextRect = RelativeRect(frame, 0.24, 0.13, 0.52, 0.16);
        var baitFButtonRect = RelativeRect(frame, 0.84, 0.76, 0.16, 0.24);
        var resultCloseTipRect = RelativeRect(frame, 0.25, 0.78, 0.50, 0.20);
        var caughtRect = FishWeightRect(frame);

        using var mat = BitmapMatConverter.ToBgraMat(frame);
        var fishBar = FindFishBar(frame, mat, meterRect);
        var fishPointer = FindFishPointer(mat, meterRect);
        if (!preferFightFastPath && fishPointer is null)
        {
            fishPointer = templates.FindCenter("FishingPoint", frame, meterRect, minSimilarity: 0.68);
        }

        if (fishBar is not null && fishPointer is not null)
        {
            return new FrameFacts(
                FishBarBounds: fishBar,
                FishBarCenter: new Point(fishBar.Value.Left + fishBar.Value.Width / 2, fishBar.Value.Top + fishBar.Value.Height / 2),
                FishPointerCenter: fishPointer,
                TakesBait: false,
                FishCaught: false,
                ResultCloseTipVisible: false,
                StartFishingButtonVisible: false,
                StartFishingButtonCenter: null,
                BaitEmptyTipVisible: false,
                CenterTip: false,
                FishingEntryVisible: false,
                MoonCardVisible: false,
                LoginAnnouncementVisible: false);
        }

        if (preferFightFastPath)
        {
            return new FrameFacts(
                FishBarBounds: fishBar,
                FishBarCenter: fishBar is null ? null : new Point(fishBar.Value.Left + fishBar.Value.Width / 2, fishBar.Value.Top + fishBar.Value.Height / 2),
                FishPointerCenter: fishPointer,
                TakesBait: false,
                FishCaught: false,
                ResultCloseTipVisible: false,
                StartFishingButtonVisible: false,
                StartFishingButtonCenter: null,
                BaitEmptyTipVisible: false,
                CenterTip: false,
                FishingEntryVisible: false,
                MoonCardVisible: false,
                LoginAnnouncementVisible: false);
        }

        var startButtonCenter = templates.FindCenter("StartFishingButton", frame, startButtonRect, minSimilarity: 0.55);
        var takesBaitText = templates.Exists("TakesBaitText", frame, baitTextRect, minSimilarity: 0.68);
        var fishingFReady = templates.Exists("FishingReadyFButton", frame, baitFButtonRect, minSimilarity: 0.68);
        var resultCloseTip = templates.Exists("ResultCloseTip", frame, resultCloseTipRect, minSimilarity: 0.58)
            || FindResultCloseTipByWhiteText(mat, resultCloseTipRect);

        return new FrameFacts(
            FishBarBounds: fishBar,
            FishBarCenter: fishBar is null ? null : new Point(fishBar.Value.Left + fishBar.Value.Width / 2, fishBar.Value.Top + fishBar.Value.Height / 2),
            FishPointerCenter: fishPointer,
            TakesBait: takesBaitText && fishingFReady,
            FishCaught: templates.Exists("FishWeightGram", frame, caughtRect),
            ResultCloseTipVisible: resultCloseTip,
            StartFishingButtonVisible: startButtonCenter is not null,
            StartFishingButtonCenter: startButtonCenter,
            BaitEmptyTipVisible: templates.FindCenterScaled("BaitEmptyTip", frame, centerTipRect, minSimilarity: 0.64, scales: BaitEmptyTipScales) is not null,
            CenterTip: false,
            FishingEntryVisible: false,
            MoonCardVisible: false,
            LoginAnnouncementVisible: false);
    }

    private static Rectangle? FindFishBar(Bitmap frame, Mat sourceMat, Rectangle searchRect)
    {
        searchRect.Intersect(new Rectangle(Point.Empty, frame.Size));
        if (searchRect.Width <= 0 || searchRect.Height <= 0)
        {
            return null;
        }

        using var cropSource = new Mat(sourceMat, new Rect(searchRect.X, searchRect.Y, searchRect.Width, searchRect.Height));
        using var crop = new Mat();
        if (cropSource.Channels() == 4)
        {
            Cv2.CvtColor(cropSource, crop, ColorConversionCodes.BGRA2BGR);
        }
        else
        {
            cropSource.CopyTo(crop);
        }

        using var hsv = new Mat();
        Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
        var (lower, upper) = GetFishBarHsvRange();
        using var hueMask = new Mat();
        using var colorMask = new Mat();
        using var mask = new Mat();
        Cv2.InRange(hsv, lower, upper, hueMask);
        Cv2.InRange(crop, new Scalar(125, 150, 0), new Scalar(245, 255, 125), colorMask);
        Cv2.BitwiseAnd(hueMask, colorMask, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 1));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        var best = FindHorizontalFishBar(mask, searchRect);
        if (best is null)
        {
            return null;
        }

        var rect = best.Value;
        return new Rectangle(searchRect.X + rect.X, searchRect.Y + rect.Y, rect.Width, rect.Height);
    }

    private static Point? FindFishPointer(Mat sourceMat, Rectangle searchRect)
    {
        searchRect.Intersect(new Rectangle(0, 0, sourceMat.Width, sourceMat.Height));
        if (searchRect.Width <= 0 || searchRect.Height <= 0)
        {
            return null;
        }

        using var cropSource = new Mat(sourceMat, new Rect(searchRect.X, searchRect.Y, searchRect.Width, searchRect.Height));
        using var crop = new Mat();
        if (cropSource.Channels() == 4)
        {
            Cv2.CvtColor(cropSource, crop, ColorConversionCodes.BGRA2BGR);
        }
        else
        {
            cropSource.CopyTo(crop);
        }

        using var hsv = new Mat();
        using var hueMask = new Mat();
        using var colorMask = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, new Scalar(15, 90, 120), new Scalar(38, 255, 255), hueMask);
        Cv2.InRange(crop, new Scalar(35, 125, 145), new Scalar(205, 255, 255), colorMask);
        Cv2.BitwiseAnd(hueMask, colorMask, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 3));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        Cv2.FindContours(mask, out CvPoint[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var best = contours
            .Select(contour => new FishPointerCandidate(Cv2.BoundingRect(contour), Cv2.ContourArea(contour)))
            .Where(candidate => IsLikelyFishPointer(candidate, searchRect))
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();
        if (best is null)
        {
            return null;
        }

        return new Point(searchRect.X + best.Rect.X + best.Rect.Width / 2, searchRect.Y + best.Rect.Y + best.Rect.Height / 2);
    }

    private sealed record FishPointerCandidate(Rect Rect, double Area)
    {
        public double Score => Area + Rect.Height * 3;
    }

    private static bool IsLikelyFishPointer(FishPointerCandidate candidate, Rectangle searchRect)
    {
        var rect = candidate.Rect;
        var minWidth = Math.Max(3, (int)(searchRect.Width * 0.004));
        var maxWidth = Math.Max(minWidth, (int)(searchRect.Width * 0.026));
        var minHeight = Math.Max(7, (int)(searchRect.Height * 0.14));
        var maxHeight = Math.Max(minHeight, (int)(searchRect.Height * 0.62));
        if (rect.Width < minWidth || rect.Width > maxWidth || rect.Height < minHeight || rect.Height > maxHeight)
        {
            return false;
        }

        if (rect.Height / (double)rect.Width < 1.2)
        {
            return false;
        }

        var centerY = rect.Y + rect.Height / 2.0;
        if (centerY < searchRect.Height * 0.10 || centerY > searchRect.Height * 0.80)
        {
            return false;
        }

        var fillRatio = candidate.Area / Math.Max(1.0, rect.Width * rect.Height);
        return fillRatio >= 0.25;
    }

    private static Rectangle? FindHorizontalFishBar(Mat mask, Rectangle searchRect)
    {
        var minRunWidth = Math.Max(30, (int)(searchRect.Width * 0.035));
        var maxRunWidth = Math.Max(minRunWidth, (int)(searchRect.Width * 0.38));
        var minRows = Math.Max(4, (int)(searchRect.Height * 0.16));
        var maxHeight = Math.Max(minRows, (int)(searchRect.Height * 0.72));
        var groups = new List<FishBarRunGroup>();

        for (var y = 0; y < mask.Rows; y++)
        {
            var runs = FindMaskRuns(mask, y, minRunWidth, maxRunWidth);
            foreach (var run in runs)
            {
                var group = groups
                    .Where(candidate => y - candidate.LastY <= 1 && candidate.Overlaps(run))
                    .OrderBy(candidate => Math.Abs(candidate.CenterX - run.CenterX))
                    .FirstOrDefault();

                if (group is null)
                {
                    groups.Add(new FishBarRunGroup(run, y));
                    continue;
                }

                group.Add(run, y);
            }
        }

        var best = groups
            .Where(group => group.RowCount >= minRows)
            .Select(group => group.ToRectangle())
            .Where(rect => IsLikelyHorizontalFishBar(rect, maxHeight))
            .OrderByDescending(rect => rect.Width * rect.Height)
            .FirstOrDefault();

        return best.Width == 0 || best.Height == 0 ? null : best;
    }

    private static List<FishBarRun> FindMaskRuns(Mat mask, int y, int minRunWidth, int maxRunWidth)
    {
        var runs = new List<FishBarRun>();
        var start = -1;
        for (var x = 0; x < mask.Cols; x++)
        {
            var active = mask.At<byte>(y, x) != 0;
            if (active && start < 0)
            {
                start = x;
            }
            else if (!active && start >= 0)
            {
                AddRunIfValid(runs, start, x - 1, minRunWidth, maxRunWidth);
                start = -1;
            }
        }

        if (start >= 0)
        {
            AddRunIfValid(runs, start, mask.Cols - 1, minRunWidth, maxRunWidth);
        }

        return runs;
    }

    private static void AddRunIfValid(List<FishBarRun> runs, int left, int right, int minRunWidth, int maxRunWidth)
    {
        var width = right - left + 1;
        if (width >= minRunWidth && width <= maxRunWidth)
        {
            runs.Add(new FishBarRun(left, right));
        }
    }

    private static bool IsLikelyHorizontalFishBar(Rectangle rect, int maxHeight)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || rect.Height > maxHeight)
        {
            return false;
        }

        return rect.Width / (double)rect.Height >= 7.0;
    }

    private static (Scalar lower, Scalar upper) GetFishBarHsvRange()
    {
        var hsv = RgbToHsv(FishBarRgb);
        return (
            new Scalar(Math.Max(0, hsv.Val0 - 9), 95, 120),
            new Scalar(Math.Min(179, hsv.Val0 + 9), 255, 255));
    }

    private readonly record struct FishBarRun(int Left, int Right)
    {
        public int Width => Right - Left + 1;
        public double CenterX => Left + Width / 2.0;
    }

    private sealed class FishBarRunGroup
    {
        private int _left;
        private int _right;
        private int _top;
        private int _bottom;

        public FishBarRunGroup(FishBarRun run, int y)
        {
            _left = run.Left;
            _right = run.Right;
            _top = y;
            _bottom = y;
            RowCount = 1;
            LastY = y;
        }

        public int RowCount { get; private set; }
        public int LastY { get; private set; }
        public double CenterX => _left + (_right - _left + 1) / 2.0;

        public void Add(FishBarRun run, int y)
        {
            _left = Math.Min(_left, run.Left);
            _right = Math.Max(_right, run.Right);
            _bottom = Math.Max(_bottom, y);
            RowCount++;
            LastY = y;
        }

        public bool Overlaps(FishBarRun run)
        {
            var overlap = Math.Min(_right, run.Right) - Math.Max(_left, run.Left) + 1;
            return overlap >= Math.Min(_right - _left + 1, run.Width) * 0.65
                || Math.Abs(CenterX - run.CenterX) <= 12;
        }

        public Rectangle ToRectangle()
        {
            return Rectangle.FromLTRB(_left, _top, _right + 1, _bottom + 1);
        }
    }

    private static bool FindWhitePrompt(Mat sourceMat, Rectangle searchRect)
    {
        searchRect.Intersect(new Rectangle(0, 0, sourceMat.Width, sourceMat.Height));
        if (searchRect.Width <= 0 || searchRect.Height <= 0)
        {
            return false;
        }

        using var crop = new Mat(sourceMat, new Rect(searchRect.X, searchRect.Y, searchRect.Width, searchRect.Height));
        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, new Scalar(0, 0, 205), new Scalar(179, 60, 255), mask);
        return Cv2.CountNonZero(mask) > 40;
    }

    private static bool FindResultCloseTipByWhiteText(Mat sourceMat, Rectangle searchRect)
    {
        searchRect.Intersect(new Rectangle(0, 0, sourceMat.Width, sourceMat.Height));
        if (searchRect.Width <= 0 || searchRect.Height <= 0)
        {
            return false;
        }

        using var crop = new Mat(sourceMat, new Rect(searchRect.X, searchRect.Y, searchRect.Width, searchRect.Height));
        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, new Scalar(0, 0, 210), new Scalar(179, 80, 255), mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        Cv2.FindContours(mask, out CvPoint[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var totalArea = 0.0;
        var wideTextLikeParts = 0;
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            var area = Cv2.ContourArea(contour);
            if (rect.Width < 4 || rect.Height < 6 || area < 8)
            {
                continue;
            }

            totalArea += area;
            if (rect.Width >= 8 && rect.Height >= 10)
            {
                wideTextLikeParts++;
            }
        }

        return totalArea >= 350 && wideTextLikeParts >= 6;
    }

    private static Scalar RgbToHsv(Scalar rgb)
    {
        using var rgbPixel = new Mat(1, 1, MatType.CV_8UC3, new Scalar(rgb.Val2, rgb.Val1, rgb.Val0));
        using var hsvPixel = new Mat();
        Cv2.CvtColor(rgbPixel, hsvPixel, ColorConversionCodes.BGR2HSV);
        var hsv = hsvPixel.At<Vec3b>(0, 0);
        return new Scalar(hsv.Item0, hsv.Item1, hsv.Item2);
    }

    private static (Scalar lower, Scalar upper) GetHsvRange(Scalar hsv, bool lowRange)
    {
        var hRange = lowRange ? 5 : 7;
        var sRange = lowRange ? 10 : 20;
        var vRange = lowRange ? 10 : 20;
        return (
            new Scalar(Math.Max(0, hsv.Val0 - hRange), Math.Max(0, hsv.Val1 - sRange), Math.Max(0, hsv.Val2 - vRange)),
            new Scalar(Math.Min(179, hsv.Val0 + hRange), Math.Min(255, hsv.Val1 + sRange), Math.Min(255, hsv.Val2 + vRange)));
    }

    private static Rectangle RelativeRect(Bitmap image, double x, double y, double width, double height)
    {
        return new Rectangle(
            (int)(image.Width * x),
            (int)(image.Height * y),
            Math.Max(1, (int)(image.Width * width)),
            Math.Max(1, (int)(image.Height * height)));
    }

    private static Rectangle FishingMeterRect(Bitmap image)
    {
        var ratio = image.Width / (double)image.Height;
        return ratio switch
        {
            <= 1.65 => TemplateRect(image, 793, 105, 986, 65, 2560, 1600),
            <= 1.85 => TemplateRect(image, 597, 102, 738, 22, 1920, 1080),
            <= 2.7 => TemplateRect(image, 640, 82, 522, 17, 1792, 768),
            _ => TemplateRect(image, 1552, 99, 740, 24, 3840, 1080),
        };
    }

    private static Rectangle FishWeightRect(Bitmap image)
    {
        var ratio = image.Width / (double)image.Height;
        return ratio switch
        {
            <= 1.65 => TemplateRect(image, 1154, 1042, 493, 193, 2560, 1600),
            <= 1.85 => TemplateRect(image, 867, 711, 389, 150, 1920, 1080),
            <= 2.7 => TemplateRect(image, 839, 535, 250, 90, 1792, 768),
            _ => TemplateRect(image, 1781, 733, 438, 125, 3840, 1080),
        };
    }

    private static Rectangle TemplateRect(Bitmap image, double x, double y, double width, double height, double baseWidth, double baseHeight)
    {
        y -= 39;
        return new Rectangle(
            (int)(image.Width * (x / baseWidth)),
            (int)(image.Height * (y / baseHeight)),
            Math.Max(1, (int)(image.Width * (width / baseWidth))),
            Math.Max(1, (int)(image.Height * (height / baseHeight))));
    }
}
