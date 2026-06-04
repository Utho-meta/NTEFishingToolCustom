namespace NTEFishingToolCustom;

using NTEFishingToolCustom.Automation;
using NTEFishingToolCustom.Native;

public partial class Form1 : Form
{
    private readonly FishingEngine _engine;
    private readonly TextBox _processName = new();
    private readonly NumericUpDown _centerTolerance = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly TextBox _logBox = new();
    private readonly System.Windows.Forms.Timer _hotkeyPollTimer = new();
    private bool _isRunning;

    public Form1()
    {
        InitializeComponent();
        _engine = new FishingEngine(AppendLog);
        _engine.Stopped += OnEngineStopped;
        BuildUi();
        ConfigureHotkeyPolling();
        FormClosing += (_, _) => ShutdownEngine();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8),
        };

        _processName.Text = "HTGame";
        _processName.Width = 160;

        _centerTolerance.Minimum = 5;
        _centerTolerance.Maximum = 120;
        _centerTolerance.Value = 30;
        _centerTolerance.Width = 72;

        settings.Controls.Add(new Label { Text = "Process", AutoSize = true, Padding = new Padding(0, 7, 6, 0) });
        settings.Controls.Add(_processName);
        settings.Controls.Add(new Label { Text = "Tolerance", AutoSize = true, Padding = new Padding(14, 7, 6, 0) });
        settings.Controls.Add(_centerTolerance);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 6),
        };

        _startButton.Text = "Start";
        _startButton.Width = 110;
        _startButton.Height = 34;
        _startButton.Click += (_, _) => StartEngine();

        _stopButton.Text = "Stop";
        _stopButton.Width = 110;
        _stopButton.Height = 34;
        _stopButton.Enabled = false;
        _stopButton.Click += (_, _) => StopEngine();

        controls.Controls.Add(_startButton);
        controls.Controls.Add(_stopButton);

        var help = new Label
        {
            Text = "CapsLock ON: run / OFF: stop",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8),
        };

        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Font = new Font(FontFamily.GenericMonospace, 9);

        root.Controls.Add(settings, 0, 0);
        root.Controls.Add(controls, 0, 1);
        root.Controls.Add(help, 0, 2);
        root.Controls.Add(_logBox, 0, 3);
        Controls.Add(root);
    }

    private void StartEngine()
    {
        var settings = new AutomationSettings
        {
            ProcessName = _processName.Text.Trim(),
            FishCenterTolerance = (int)_centerTolerance.Value,
        };

        _engine.Start(settings);
        SetRunningState(true);
    }

    private void StopEngine()
    {
        _engine.Stop();
        SetRunningState(false);
    }

    private void ConfigureHotkeyPolling()
    {
        _hotkeyPollTimer.Interval = 100;
        _hotkeyPollTimer.Tick += (_, _) =>
        {
            var shouldRun = GlobalHotkey.IsCapsLockOn();
            if (shouldRun && !_isRunning)
            {
                AppendLog("CapsLock ON: started");
                StartEngine();
            }
            else if (!shouldRun && _isRunning)
            {
                AppendLog("CapsLock OFF: stopped");
                StopEngine();
            }
        };
        _hotkeyPollTimer.Start();
    }

    private void ShutdownEngine()
    {
        _hotkeyPollTimer.Stop();
        _engine.Stopped -= OnEngineStopped;
        _engine.Stop();
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _logBox.AppendText(line);
        WriteLogFile(line);
    }

    private void OnEngineStopped()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnEngineStopped);
            return;
        }

        SetRunningState(false);
    }

    private void SetRunningState(bool isRunning)
    {
        _isRunning = isRunning;
        _startButton.Enabled = !isRunning;
        _stopButton.Enabled = isRunning;
    }

    private static void WriteLogFile(string line)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "NTEFishingToolCustom.log"), line);
        }
        catch
        {
        }
    }
}
