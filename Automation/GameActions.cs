namespace NTEFishingToolCustom.Automation;

using System.Drawing;
using NTEFishingToolCustom.Imaging;
using NTEFishingToolCustom.Native;

public sealed class GameActions
{
    private readonly IntPtr _window;
    private readonly Action<string> _log;
    private readonly Func<bool> _shouldStop;
    private readonly TemplateLibrary _templates;
    private byte _heldKey;

    private static readonly PointF MaxQuantity = new(0.952f, 0.845f);
    private static readonly PointF PurchaseButton = new(0.839f, 0.909f);
    private static readonly PointF PurchaseConfirmButton = new(0.601f, 0.645f);
    private static readonly PointF ChangeBaitButton = new(0.610f, 0.640f);
    private static readonly PointF StartFishingButton = new(0.840f, 0.835f);
    private static readonly PointF ResultCloseBlankArea = StartFishingButton;
    private static readonly PointF NeutralBlankArea = StartFishingButton;
    private const int ShopOpenDelayMs = 2000;
    private static readonly RectangleF[] ShopBaitSlots =
    {
        new(0.025f, 0.110f, 0.102f, 0.190f),
        new(0.138f, 0.110f, 0.102f, 0.190f),
        new(0.252f, 0.110f, 0.102f, 0.190f),
        new(0.025f, 0.312f, 0.102f, 0.190f),
        new(0.138f, 0.312f, 0.102f, 0.190f),
        new(0.252f, 0.312f, 0.102f, 0.190f),
    };
    private static readonly double[] ShopCompactBaitScales = { 2.2, 2.4, 2.6, 2.8, 3.0, 3.2, 3.4 };
    private static readonly double[] ShopPriceScales = { 0.82, 0.90, 0.98, 1.06, 1.14, 1.22 };
    private const double MinFiveCoinBaitScore = 0.70;
    private const double MinFiveCoinPriceScore = 0.62;

    public GameActions(IntPtr window, Action<string> log, Func<bool>? shouldStop = null)
    {
        _window = window;
        _log = log;
        _shouldStop = shouldStop ?? (() => false);
        _templates = new TemplateLibrary(AppContext.BaseDirectory, log);
    }

    public void KeepPointerNearFishBarCenter(Rectangle fishBarBounds, int pointerX, int tolerance)
    {
        if (_shouldStop())
        {
            ReleaseHeldDirection();
            return;
        }

        var center = fishBarBounds.Left + fishBarBounds.Width / 2;
        var distance = Math.Abs(center - pointerX);

        if (distance < tolerance)
        {
            ReleaseHeldDirection();
            return;
        }

        if (center > pointerX)
        {
            HoldDirection(ScanCode.D);
        }
        else
        {
            HoldDirection(ScanCode.A);
        }
    }

    public void CastOrHook()
    {
        if (_shouldStop())
        {
            return;
        }

        ReleaseHeldDirection();
        InputSimulator.Press(ScanCode.F);
        _log("Press F: cast / hook");
    }

    public void ClickStartFishing(Point? detectedCenter = null)
    {
        if (_shouldStop())
        {
            return;
        }

        ReleaseHeldDirection();
        if (detectedCenter is not null)
        {
            ClickClientPoint(detectedCenter.Value, "start fishing detected center", holdMs: 650, repeat: 2);
            return;
        }

        ClickClientRatio(StartFishingButton, "start fishing", holdMs: 650, repeat: 2);
    }

    public void CloseCaughtResult()
    {
        ReleaseHeldDirection();
        ClickClientRatio(ResultCloseBlankArea, "close fish result blank area", holdMs: 120);
    }

    public void ClickBlankRecovery()
    {
        ReleaseHeldDirection();
        ClickClientRatio(NeutralBlankArea, "blank recovery", holdMs: 80);
    }

    public void CloseCaughtResultAndCast()
    {
        CloseCaughtResult();
        if (!Wait(500))
        {
            return;
        }

        InputSimulator.Press(ScanCode.F);
        _log("Press F: continue fishing");
    }

    public void Back()
    {
        if (_shouldStop())
        {
            return;
        }

        ReleaseHeldDirection();
        InputSimulator.Press(ScanCode.Escape);
        _log("Press ESC: back");
    }

    public void ChangeBait()
    {
        if (_shouldStop())
        {
            return;
        }

        ReleaseHeldDirection();
        InputSimulator.Press(ScanCode.E);
        _log("Press E: change bait");
    }

    public void OpenShop()
    {
        if (_shouldStop())
        {
            return;
        }

        ReleaseHeldDirection();
        InputSimulator.Press(ScanCode.R);
        _log("Press R: open shop");
    }

    public void BuyFiveCoinUniversalBaitAndEquip()
    {
        ReleaseHeldDirection();
        if (_shouldStop())
        {
            return;
        }

        _log("Bait empty: buy five-coin universal bait");
        OpenShop();
        _log("Wait 2s for shop to load");
        if (!Wait(ShopOpenDelayMs))
        {
            return;
        }

        if (!ClickFiveCoinUniversalBait())
        {
            _log("Five-coin universal bait was not found; abort purchase");
            Back();
            Wait(800);
            return;
        }

        if (!Wait(700))
        {
            return;
        }

        ClickClientRatio(MaxQuantity, "max quantity");
        if (!Wait(700))
        {
            return;
        }

        ClickClientRatio(PurchaseButton, "purchase");
        if (!Wait(1500))
        {
            return;
        }

        ClickClientRatio(PurchaseConfirmButton, "confirm purchase");
        if (!Wait(3000))
        {
            return;
        }

        Back();
        if (!Wait(1200))
        {
            return;
        }

        Back();
        if (!Wait(1500))
        {
            return;
        }

        ChangeBait();
        if (!Wait(1800))
        {
            return;
        }

        ClickClientRatio(ChangeBaitButton, "equip bait");
        Wait(1200);
    }

    public void ReleaseHeldDirection()
    {
        if (_heldKey == 0)
        {
            return;
        }

        InputSimulator.KeyUp(_heldKey);
        _heldKey = 0;
    }

    public void ResetFishingControl()
    {
        ReleaseHeldDirection();
    }

    public void Note(string message)
    {
        _log(message);
    }

    private void HoldDirection(byte scanCode)
    {
        if (_heldKey == scanCode)
        {
            return;
        }

        ReleaseHeldDirection();
        InputSimulator.KeyDown(scanCode);
        _heldKey = scanCode;
    }

    private void ClickClientRatio(PointF ratio, string label, int holdMs = 160, int repeat = 1)
    {
        if (_shouldStop())
        {
            return;
        }

        if (!WindowApi.GetPureClientRect(_window, out var rect))
        {
            _log($"Click failed: {label}");
            return;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var point = new Point(
            rect.Left + (int)(width * ratio.X),
            rect.Top + (int)(height * ratio.Y));

        ClickScreenPoint(point, label, holdMs, repeat);
    }

    private void ClickClientPoint(Point clientPoint, string label, int holdMs = 160, int repeat = 1)
    {
        if (_shouldStop())
        {
            return;
        }

        if (!WindowApi.GetPureClientRect(_window, out var rect))
        {
            _log($"Click failed: {label}");
            return;
        }

        var point = new Point(rect.Left + clientPoint.X, rect.Top + clientPoint.Y);
        ClickScreenPoint(point, label, holdMs, repeat);
    }

    private bool ClickFiveCoinUniversalBait()
    {
        if (_shouldStop())
        {
            return false;
        }

        using var frame = WindowCapture.Capture(_window);
        if (frame is not null)
        {
            Point? bestSlotCenter = null;
            var bestScore = double.MinValue;

            for (var i = 0; i < ShopBaitSlots.Length; i++)
            {
                var slot = ToFrameRect(frame, ShopBaitSlots[i]);
                var priceRect = new Rectangle(
                    slot.Left + (int)(slot.Width * 0.08),
                    slot.Top + (int)(slot.Height * 0.72),
                    Math.Max(1, (int)(slot.Width * 0.84)),
                    Math.Max(1, (int)(slot.Height * 0.24)));

                var baitMatch = _templates.FindBestScaled(
                    "FiveCoinUniversalBaitInner",
                    frame,
                    slot,
                    scales: ShopCompactBaitScales);

                var priceMatch = _templates.FindBestScaled(
                    "FiveCoinPrice",
                    frame,
                    priceRect,
                    scales: ShopPriceScales);

                var baitScore = baitMatch?.Similarity ?? 0;
                var priceScore = priceMatch?.Similarity ?? 0;
                _log($"Shop slot {i + 1}: bait {baitScore:0.00}, price {priceScore:0.00}");

                if (baitScore < MinFiveCoinBaitScore || priceScore < MinFiveCoinPriceScore)
                {
                    continue;
                }

                var center = new Point(slot.Left + slot.Width / 2, slot.Top + slot.Height / 2);
                var score = baitScore + priceScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlotCenter = center;
                }
            }

            if (bestSlotCenter is not null)
            {
                ClickClientPoint(bestSlotCenter.Value, $"select five-coin universal bait score {bestScore:0.00}");
                return true;
            }
        }

        _log("Five-coin universal bait not found in first two shop rows");
        return false;
    }

    private static Rectangle ToFrameRect(Bitmap frame, RectangleF ratio)
    {
        return new Rectangle(
            Math.Max(0, (int)(frame.Width * ratio.X)),
            Math.Max(0, (int)(frame.Height * ratio.Y)),
            Math.Max(1, (int)(frame.Width * ratio.Width)),
            Math.Max(1, (int)(frame.Height * ratio.Height)));
    }

    private void ClickScreenPoint(Point point, string label, int holdMs, int repeat)
    {
        InputSimulator.Click(point, holdMs, repeat);
        _log($"Click: {label} at {point.X},{point.Y}");
    }

    private bool Wait(int milliseconds)
    {
        var end = DateTime.Now.AddMilliseconds(milliseconds);
        while (DateTime.Now < end)
        {
            if (_shouldStop())
            {
                ReleaseHeldDirection();
                return false;
            }

            Thread.Sleep(Math.Min(50, Math.Max(1, (int)(end - DateTime.Now).TotalMilliseconds)));
        }

        return true;
    }
}
