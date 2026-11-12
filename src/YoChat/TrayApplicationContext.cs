using System.Media;
using YoChat.Audio;
using YoChat.Core;
using YoChat.Infrastructure;
using YoChat.Speech;
using YoChat.UI;

namespace YoChat;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore;
    private readonly IWakePhraseRecognizer _recognizer;
    private readonly IHotkeyDispatcher _hotkeyDispatcher;
    private readonly IChatGptCaptureDetector _captureDetector;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly Control _uiDispatcher = new();
    private readonly System.Windows.Forms.Timer _acknowledgementTimer = new() { Interval = 1200 };
    private readonly Icon _listeningIcon = TrayIconFactory.Create(Color.LimeGreen);
    private readonly Icon _pausedIcon = TrayIconFactory.Create(Color.Goldenrod);
    private readonly Icon _errorIcon = TrayIconFactory.Create(Color.IndianRed);
    private readonly Icon _acknowledgementIcon = TrayIconFactory.Create(Color.DeepSkyBlue);

    private AppSettings _settings;
    private WakePhraseGate _gate;
    private bool _paused;
    private bool _exiting;
    private readonly BubbleEngine _bubbles;

    public TrayApplicationContext()
        : this(
            new SettingsStore(),
            new WindowsSpeechRecognizer(),
            new WindowsHotkeyDispatcher(),
            new WindowsChatGptCaptureDetector())
    {
    }

    internal TrayApplicationContext(
        SettingsStore settingsStore,
        IWakePhraseRecognizer recognizer,
        IHotkeyDispatcher hotkeyDispatcher,
        IChatGptCaptureDetector captureDetector)
    {
        _settingsStore = settingsStore;
        _recognizer = recognizer;
        _hotkeyDispatcher = hotkeyDispatcher;
        _captureDetector = captureDetector;
        _settings = _settingsStore.Load();
        _gate = CreateGate(_settings);
        _bubbles = new BubbleEngine(() => _settings.Copy());

        _uiDispatcher.CreateControl();
        _statusItem = new ToolStripMenuItem("Starting…") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Pause listening");
        _pauseItem.Click += (_, _) => TogglePaused();

        var settingsItem = new ToolStripMenuItem("Settings…");
        settingsItem.Click += (_, _) => ShowSettings();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        var bubbleStatus = new ToolStripMenuItem("Bubbles: starting") { Enabled = false };
        menu.Opening += (_, _) => bubbleStatus.Text = _bubbles.Status;
        menu.Items.AddRange(
        [
            _statusItem,
            bubbleStatus,
            new ToolStripSeparator(),
            _pauseItem,
            settingsItem,
            new ToolStripSeparator(),
            exitItem
        ]);

        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _pausedIcon,
            Text = "YoChat — starting",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _acknowledgementTimer.Tick += (_, _) =>
        {
            _acknowledgementTimer.Stop();
            if (!_paused)
            {
                SetStatus("Listening", _listeningIcon);
            }
        };

        _recognizer.Detected += RecognitionDetected;
        _recognizer.StatusChanged += RecognitionStatusChanged;
        StartListening(showStartupMessage: true);
    }

    protected override void ExitThreadCore()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _acknowledgementTimer.Stop();
        _recognizer.Dispose();
        _bubbles.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _uiDispatcher.Dispose();
        _listeningIcon.Dispose();
        _pausedIcon.Dispose();
        _errorIcon.Dispose();
        _acknowledgementIcon.Dispose();
        base.ExitThreadCore();
    }

    private void StartListening(bool showStartupMessage = false)
    {
        try
        {
            _recognizer.Start(_settings.WakePhrase);
            _paused = false;
            _pauseItem.Text = "Pause listening";
            SetStatus($"Listening for “{_settings.WakePhrase}”", _listeningIcon);

            if (showStartupMessage)
            {
                _trayIcon.ShowBalloonTip(
                    2500,
                    "YoChat is listening",
                    $"Say “{_settings.WakePhrase}”. Right-click the tray icon for settings.",
                    ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            _paused = true;
            _pauseItem.Text = "Resume listening";
            SetStatus("Microphone or wake model unavailable", _errorIcon);
            _trayIcon.ShowBalloonTip(
                5000,
                "YoChat could not start listening",
                $"{ex.Message} Check microphone permissions and that the complete wake model is installed.",
                ToolTipIcon.Error);
        }
    }

    private void TogglePaused()
    {
        if (_paused)
        {
            StartListening();
            return;
        }

        _recognizer.Stop();
        _paused = true;
        _pauseItem.Text = "Resume listening";
        SetStatus("Paused", _pausedIcon);
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings.Copy());
        if (form.ShowDialog() != DialogResult.OK || form.Result is null)
        {
            return;
        }

        var wasPaused = _paused;
        _recognizer.Stop();
        var newSettings = form.Result.Normalized();
        try
        {
            _settingsStore.Save(newSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _trayIcon.ShowBalloonTip(4000, "YoChat settings error", ex.Message, ToolTipIcon.Error);
            if (!wasPaused)
            {
                StartListening();
            }
            return;
        }

        _settings = newSettings;
        _gate = CreateGate(_settings);

        if (wasPaused)
        {
            _paused = true;
            _pauseItem.Text = "Resume listening";
            SetStatus("Paused", _pausedIcon);
        }
        else
        {
            StartListening();
        }
    }

    private void RecognitionDetected(object? sender, WakePhraseDetectedEventArgs e)
    {
        var gate = _gate;
        if (_paused || !gate.TryAccept(e.Text, e.Confidence, DateTimeOffset.UtcNow))
        {
            return;
        }

        RunOnUi(() =>
        {
            if (_paused || _exiting || !ReferenceEquals(gate, _gate)) return;
            try
            {
                var captureStatus = _captureDetector.GetStatus();
                switch (WakeHandoffPolicy.Decide(captureStatus))
                {
                    case WakeHandoffDecision.SuppressWhileChatGptCaptures:
                        SetTransientStatus(
                            "ChatGPT microphone active — wake ignored",
                            _acknowledgementIcon);
                        return;
                    case WakeHandoffDecision.SuppressBecauseStateIsUnknown:
                        SetStatus("Cannot verify ChatGPT microphone state", _errorIcon);
                        _trayIcon.ShowBalloonTip(
                            4000,
                            "YoChat did not send the hotkey",
                            captureStatus.Detail is { Length: > 0 }
                                ? $"Windows audio-session state was unavailable: {captureStatus.Detail}"
                                : "Windows audio-session state was unavailable.",
                            ToolTipIcon.Warning);
                        return;
                }

                if (!HotkeyParser.TryParse(_settings.Hotkey, out var hotkey, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                _bubbles.BeginCall();
                _hotkeyDispatcher.Send(hotkey!);
                SystemSounds.Asterisk.Play();
                SetTransientStatus("Wake phrase heard — hotkey sent", _acknowledgementIcon);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                SetStatus("Could not send hotkey", _errorIcon);
                _trayIcon.ShowBalloonTip(4000, "YoChat hotkey error", ex.Message, ToolTipIcon.Error);
            }
        });
    }

    private void RecognitionStatusChanged(object? sender, string status)
    {
        if (status.StartsWith("Recognition error:", StringComparison.Ordinal))
        {
            RunOnUi(() => SetStatus(status, _errorIcon));
        }
    }

    private void SetStatus(string status, Icon icon)
    {
        _statusItem.Text = status;
        _trayIcon.Text = status.Length <= 120 ? $"YoChat — {status}" : "YoChat";
        _trayIcon.Icon = icon;
    }

    private void SetTransientStatus(string status, Icon icon)
    {
        SetStatus(status, icon);
        _acknowledgementTimer.Stop();
        _acknowledgementTimer.Start();
    }

    private void RunOnUi(Action action)
    {
        if (_exiting || _uiDispatcher.IsDisposed)
        {
            return;
        }

        if (_uiDispatcher.InvokeRequired)
        {
            _uiDispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private static WakePhraseGate CreateGate(AppSettings settings) =>
        new(settings.WakePhrase, settings.ConfidenceThreshold, TimeSpan.FromSeconds(settings.CooldownSeconds));
}
