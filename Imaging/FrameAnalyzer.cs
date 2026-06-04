namespace NTEFishingToolCustom.Imaging;

using System.Drawing;
using NTEFishingToolCustom.Automation;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using CvPoint = OpenCvSharp.Point;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

public static class FrameAnalyzer
{
    private static readonly Scalar FishBarRgb = new(49, 218, 183);

    public static FrameFacts Analyze(Bitmap frame, IntPtr window, TemplateLibrary templates)
    {
        var meterRect = FishingMeterRect(frame);
        var centerTipRect = RelativeRect(frame, 0.28, 0.36, 0.44, 0.20);
        var startButtonRect = RelativeRect(frame, 0.56, 0.74, 0.42, 0.20);
        var baitTextRect = RelativeRect(frame, 0.24, 0.13, 0.52, 0.16);
        var baitFButtonRect = RelativeRect(frame, 0.84, 0.76, 0.16, 0.24);
        var resultCloseTipRect = RelativeRect(frame, 0.25, 0.78, 0.50, 0.20);
        var caughtRect = FishWeightRect(frame);

        using var mat = frame.ToMat();
        var fishBar = FindFishBar(frame, mat, meterRect);
        var fishPointer = templates.FindCenter("FishingPoint", frame, meterRect, minSimilarity: 0.60)
            ?? ColorDetector.FindLargestAreaCenter(frame, meterRect, RgbRange.FishPointer);
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
            BaitEmptyTipVisible: templates.Exists("BaitEmptyTip", frame, centerTipRect),
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

        using var crop = new Mat(sourceMat, new Rect(searchRect.X, searchRect.Y, searchRect.Width, searchRect.Height));
        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
        var (lower, upper) = GetHsvRange(RgbToHsv(FishBarRgb), lowRange: false);
        Cv2.InRange(hsv, lower, upper, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(20, 15));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        Cv2.FindContours(mask, out CvPoint[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
        {
            return null;
        }

        var largest = contours.OrderByDescending(contour => Cv2.ContourArea(contour)).First();
        var rect = Cv2.BoundingRect(largest);
        if (rect.Width < 4 || rect.Height < 4)
        {
            return null;
        }

        return new Rectangle(searchRect.X + rect.X, searchRect.Y + rect.Y, rect.Width, rect.Height);
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
            <= 1.65 => TemplateRect(image, 793, 122, 986, 31, 2560, 1600),
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
