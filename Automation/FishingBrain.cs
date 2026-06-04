namespace NTEFishingToolCustom.Automation;

public sealed class FishingBrain
{
    private static readonly TimeSpan FightExitGrace = TimeSpan.FromMilliseconds(1200);

    private FishingState _state = FishingState.FishingIdle;
    private DateTime _lastUsefulAction = DateTime.Now;
    private DateTime _nextAllowedAction = DateTime.MinValue;
    private bool _triedInitialStartClick;
    private bool _waitingToPressF;
    private bool _waitingForBite;
    private bool _wasFighting;
    private DateTime? _fightBarLostSince;
    private DateTime? _resultCloseTipSeenSince;
    private DateTime? _fishCaughtSeenSince;
    private DateTime _nextBiteProbeAt = DateTime.MinValue;
    private int _biteProbeCount;
    private DateTime _recoveringFromResultUntil = DateTime.MinValue;

    public FishingState State => _state;

    public void Reset()
    {
        _state = FishingState.FishingIdle;
        _lastUsefulAction = DateTime.Now;
        _nextAllowedAction = DateTime.MinValue;
        _triedInitialStartClick = false;
        _waitingToPressF = false;
        _waitingForBite = false;
        _wasFighting = false;
        _fightBarLostSince = null;
        _resultCloseTipSeenSince = null;
        _fishCaughtSeenSince = null;
        _nextBiteProbeAt = DateTime.MinValue;
        _biteProbeCount = 0;
        _recoveringFromResultUntil = DateTime.MinValue;
    }

    public void Tick(FrameFacts facts, GameActions actions, AutomationSettings settings)
    {
        if (DateTime.Now < _recoveringFromResultUntil)
        {
            actions.ResetFishingControl();
            return;
        }

        if (facts.FishBarBounds is not null && facts.FishPointerCenter is not null)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            _state = FishingState.Fighting;
            _wasFighting = true;
            _fightBarLostSince = null;
            _resultCloseTipSeenSince = null;
            _fishCaughtSeenSince = null;
            actions.KeepPointerNearFishBarCenter(
                facts.FishBarBounds.Value,
                facts.FishPointerCenter.Value.X,
                settings.FishCenterTolerance);
            MarkAction();
            return;
        }

        var leftFightStage = true;
        if (_wasFighting)
        {
            _fightBarLostSince ??= DateTime.Now;
            leftFightStage = DateTime.Now - _fightBarLostSince.Value >= FightExitGrace;
            if (!leftFightStage)
            {
                actions.ResetFishingControl();
                return;
            }
        }

        var canCloseCaughtResult = _triedInitialStartClick
            && leftFightStage
            && (_wasFighting || facts.FishCaught || _fishCaughtSeenSince is not null);
        if (canCloseCaughtResult && facts.ResultCloseTipVisible)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            _resultCloseTipSeenSince ??= DateTime.Now;
            _fishCaughtSeenSince = null;
            actions.ResetFishingControl();
            if ((DateTime.Now - _resultCloseTipSeenSince.Value).TotalSeconds >= 3)
            {
                CloseCaughtResultOnce(actions);
            }

            return;
        }

        _resultCloseTipSeenSince = null;

        if (leftFightStage && facts.FishCaught)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            _fishCaughtSeenSince ??= DateTime.Now;
            actions.ResetFishingControl();
            if ((DateTime.Now - _fishCaughtSeenSince.Value).TotalSeconds >= 10)
            {
                CloseCaughtResultOnce(actions);
            }

            return;
        }

        _fishCaughtSeenSince = null;

        if (facts.TakesBait)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            actions.CastOrHook();
            _state = FishingState.Fighting;
            MarkAction(TimeSpan.FromMilliseconds(350));
            return;
        }

        if (facts.BaitEmptyTipVisible)
        {
            HandleBaitNeeded(actions, settings);
            return;
        }

        if (_waitingForBite)
        {
            actions.ResetFishingControl();
            if (DateTime.Now >= _nextBiteProbeAt)
            {
                actions.CastOrHook();
                _biteProbeCount++;
                if (_biteProbeCount >= 15)
                {
                    HandleBaitNeeded(actions, settings);
                    return;
                }

                _nextBiteProbeAt = DateTime.Now + TimeSpan.FromSeconds(1);
            }

            return;
        }

        if (DateTime.Now < _nextAllowedAction)
        {
            return;
        }

        if (!_triedInitialStartClick)
        {
            _triedInitialStartClick = true;
            _state = FishingState.FishingIdle;
            actions.ClickStartFishing(facts.StartFishingButtonCenter);
            _waitingToPressF = true;
            MarkAction(TimeSpan.FromSeconds(3));
            return;
        }

        if (_waitingToPressF && facts.StartFishingButtonVisible)
        {
            actions.ClickStartFishing(facts.StartFishingButtonCenter);
            MarkAction(TimeSpan.FromSeconds(3));
            return;
        }

        if (_waitingToPressF && !facts.StartFishingButtonVisible)
        {
            _waitingToPressF = false;
            _state = FishingState.FishingIdle;
            actions.CastOrHook();
            StartBiteProbe();
            MarkAction(TimeSpan.FromSeconds(2));
            return;
        }

        if (facts.LoginAnnouncementVisible || facts.MoonCardVisible)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            _state = FishingState.Recovering;
            actions.Back();
            MarkAction(TimeSpan.FromSeconds(2));
            return;
        }

        if (facts.FishingEntryVisible)
        {
            _waitingToPressF = false;
            _state = FishingState.GameIdle;
            actions.CastOrHook();
            StartBiteProbe();
            MarkAction(TimeSpan.FromSeconds(2));
            return;
        }

        if (facts.StartFishingButtonVisible)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            _state = FishingState.FishingIdle;
            actions.ClickStartFishing(facts.StartFishingButtonCenter);
            _waitingToPressF = true;
            MarkAction(TimeSpan.FromSeconds(3));
            return;
        }

        actions.ResetFishingControl();

        var idleSeconds = (DateTime.Now - _lastUsefulAction).TotalSeconds;
        if (idleSeconds >= settings.ResetAfterSeconds)
        {
            _waitingForBite = false;
            _biteProbeCount = 0;
            _state = FishingState.Recovering;
            actions.Back();
            MarkAction(TimeSpan.FromSeconds(2));
            return;
        }

        if (idleSeconds >= settings.IdleCastAfterSeconds)
        {
            _state = FishingState.FishingIdle;
            actions.CastOrHook();
            StartBiteProbe();
            MarkAction(TimeSpan.FromSeconds(1));
        }
    }

    private void MarkAction(TimeSpan? cooldown = null)
    {
        _lastUsefulAction = DateTime.Now;
        _nextAllowedAction = DateTime.Now + (cooldown ?? TimeSpan.Zero);
    }

    private void CloseCaughtResultOnce(GameActions actions)
    {
        _wasFighting = false;
        _fightBarLostSince = null;
        _resultCloseTipSeenSince = null;
        _fishCaughtSeenSince = null;
        _waitingToPressF = false;
        _biteProbeCount = 0;
        _state = FishingState.Recovering;
        actions.CloseCaughtResultAndCast();
        StartBiteProbe();
        _recoveringFromResultUntil = DateTime.Now + TimeSpan.FromSeconds(2);
        _state = FishingState.FishingIdle;
        MarkAction(TimeSpan.FromSeconds(2));
    }

    private void StartBiteProbe()
    {
        _waitingForBite = true;
        _nextBiteProbeAt = DateTime.Now + TimeSpan.FromSeconds(1);
        _biteProbeCount = 0;
    }

    private void HandleBaitNeeded(GameActions actions, AutomationSettings settings)
    {
        _waitingForBite = false;
        _biteProbeCount = 0;
        _state = FishingState.NeedBait;
        if (settings.AutoBuyBait)
        {
            actions.BuyFiveCoinUniversalBaitAndEquip();
            StartBiteProbe();
        }

        MarkAction(TimeSpan.FromSeconds(3));
    }
}
