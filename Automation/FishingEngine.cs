namespace NTEFishingToolCustom.Automation;

using System.Diagnostics;
using NTEFishingToolCustom.Imaging;
using NTEFishingToolCustom.Native;

public sealed class FishingEngine
{
    private readonly Action<string> _log;
    private readonly FishingBrain _brain = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public event Action? Stopped;

    public FishingEngine(Action<string> log)
    {
        _log = log;
    }

    public void Start(AutomationSettings settings)
    {
        if (_loopTask is { IsCompleted: false })
        {
            return;
        }

        _brain.Reset();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoop(settings, _cts.Token));
        _log("Automation started");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _log("Automation stop requested");
    }

    private async Task RunLoop(AutomationSettings settings, CancellationToken token)
    {
        var process = Process.GetProcessesByName(settings.ProcessName).FirstOrDefault();
        if (process is null || process.MainWindowHandle == IntPtr.Zero)
        {
            _log($"Game window not found: {settings.ProcessName}");
            Stopped?.Invoke();
            return;
        }

        var window = process.MainWindowHandle;
        var templates = new TemplateLibrary(AppContext.BaseDirectory, _log);
        var actions = new GameActions(window, _log, () => token.IsCancellationRequested || !GlobalHotkey.IsCapsLockOn());

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!GlobalHotkey.IsCapsLockOn())
                {
                    _log("CapsLock OFF: stopped");
                    break;
                }

                WindowApi.SetForegroundWindow(window);
                using var frame = WindowCapture.Capture(window);
                if (frame is null)
                {
                    _log("Window capture failed");
                    await Task.Delay(500, token);
                    continue;
                }

                var facts = FrameAnalyzer.Analyze(frame, window, templates);
                _brain.Tick(facts, actions, settings);

                await Task.Delay(settings.LoopDelayMs, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogException(ex);
            _log($"Loop exception: {ex.GetType().Name}: {ex.Message}");
            actions.ReleaseHeldDirection();

            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            actions.ReleaseHeldDirection();
            Stopped?.Invoke();
        }
    }

    private static void LogException(Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "LoopException.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
