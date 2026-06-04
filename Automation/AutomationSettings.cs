namespace NTEFishingToolCustom.Automation;

public sealed class AutomationSettings
{
    public string ProcessName { get; init; } = "HTGame";
    public int LoopDelayMs { get; init; } = 16;
    public int FishCenterTolerance { get; init; } = 30;
    public int IdleCastAfterSeconds { get; init; } = 10;
    public int ResetAfterSeconds { get; init; } = 40;
    public bool AutoBuyBait { get; init; } = true;
    public bool AutoSellFish { get; init; } = true;
}
