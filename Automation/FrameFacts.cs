namespace NTEFishingToolCustom.Automation;

using System.Drawing;

public sealed record FrameFacts(
    Rectangle? FishBarBounds,
    Point? FishBarCenter,
    Point? FishPointerCenter,
    bool TakesBait,
    bool FishCaught,
    bool ResultCloseTipVisible,
    bool StartFishingButtonVisible,
    Point? StartFishingButtonCenter,
    bool BaitEmptyTipVisible,
    bool CenterTip,
    bool FishingEntryVisible,
    bool MoonCardVisible,
    bool LoginAnnouncementVisible);
