namespace NTEFishingToolCustom.Automation;

using System.Drawing;
using NTEFishingToolCustom.Native;

public sealed class GameActions
{
    private readonly IntPtr _window;
    private readonly Action<string> _log;
    private readonly Func<bool> _shouldStop;
    private byte _heldKey;

    private static readonly PointF FiveCoinUniversalBait = new(0.296f, 0.197f);
    private static readonly PointF MaxQuantity = new(0.952f, 0.845f);
    private static readonly PointF PurchaseButton = new(0.839f, 0.909f);
    private static readonly PointF PurchaseConfirmButton = new(0.601f, 0.645f);
    private static readonly PointF ChangeBaitButton = new(0.610f, 0.640f);
    private static readonly PointF StartFishingButton = new(0.840f, 0.835f);
    private static readonly PointF ResultBlankArea = new(0.250f, 0.250f);

    public GameActions(IntPtr window, Action<string> log, Func<bool>? shouldStop = null)
    {
        _window = window;
        _log = log;
        _shouldStop = shouldStop ?? (() => false);
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
        ClickClientRatio(ResultBlankArea, "close fish result blank area", holdMs: 120);
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
        if (!Wait(2500))
        {
            return;
        }

        ClickClientRatio(FiveCoinUniversalBait, "select five-coin universal bait");
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
