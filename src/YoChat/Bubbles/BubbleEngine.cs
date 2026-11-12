using System.IO;
using System.Text.Json;
using YoChat.Core;

namespace YoChat;

sealed class BubbleEngine : IDisposable
{
    readonly object gate = new();
    readonly CancellationTokenSource cancellation = new();
    readonly Func<AppSettings> settings;
    readonly PillLocator locator = new();
    readonly BubbleView view;
    PillPosition pill = new(false, 0, 0);
    VoiceRow[] rows = [];
    long positionAt, transcriptAt;
    double clearance = 28;
    bool enabled = true;
    int generation;
    string status = "Bubbles: waiting for voice";
    public string Status { get { lock (gate) return status; } }

    public BubbleEngine(Func<AppSettings> settings)
    {
        this.settings = settings;
        view = new BubbleView(Read, () => System.Windows.Forms.Application.Exit());
        Start(PositionLoop);
        Start(TranscriptLoop);
        Start(PromptLoop);
    }

    internal static string ResolveTranscriptPath(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        var home = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(home)) home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        return Path.Combine(home, "realtime-voice-continuity.json");
    }

    void Start(Action action)
    {
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { lock (gate) status = "Bubbles: " + e.Message; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
    }
    bool Wait(int milliseconds) => cancellation.Token.WaitHandle.WaitOne(milliseconds);
    ViewSnapshot Read()
    {
        lock (gate) return new(enabled ? pill : new(false, 0, 0), rows, clearance, positionAt, transcriptAt);
    }
    public void BeginCall()
    {
        Interlocked.Increment(ref generation);
        lock (gate) { rows = []; transcriptAt = 0; }
    }
    void PositionLoop()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var next = locator.Read();
            lock (gate) { pill = next; positionAt = Environment.TickCount64; }
            if (Wait(8)) return;
        }
    }
    void PromptLoop()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var position = Read().Pill;
            var gap = position.Visible ? PlacementModel.Clearance(position, locator.ReadObstacles()) : 28;
            lock (gate) clearance = gap;
            if (Wait(120)) return;
        }
    }
    void TranscriptLoop()
    {
        var reader = new ContinuityReader();
        int previousGeneration = -1; string previousPath = "";
        long hiddenAt = 0; bool cleared = false;
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var config = settings(); var path = ResolveTranscriptPath(config.TranscriptPath);
                var currentGeneration = Volatile.Read(ref generation);
                lock (gate) enabled = config.BubblesEnabled;
                if (previousGeneration != currentGeneration || previousPath != path || !config.BubblesEnabled)
                {
                    reader.Reset(); previousGeneration = currentGeneration; previousPath = path;
                    lock (gate) { rows = []; transcriptAt = 0; }
                }
                if (!config.BubblesEnabled) { lock (gate) status = "Bubbles: disabled in settings"; }
                else if (!File.Exists(path)) { lock (gate) status = "Bubbles: waiting for ChatGPT's voice transcript file"; }
                else
                {
                    bool visible = Read().Pill.Visible;
                    if (!visible)
                    {
                        if (hiddenAt == 0) hiddenAt = Environment.TickCount64;
                        if (!cleared && Environment.TickCount64 - hiddenAt > 1500) { reader.Reset(); cleared = true; }
                    }
                    else { hiddenAt = 0; cleared = false; }
                    reader.Update(File.ReadAllText(path));
                    lock (gate)
                    {
                        // Do not publish a snapshot from the previous call after a concurrent wake.
                        if (currentGeneration == Volatile.Read(ref generation))
                        {
                            rows = reader.Rows; transcriptAt = Environment.TickCount64;
                            status = rows.Length > 0 ? "Bubbles: direct transcript connected" : "Bubbles: waiting for the next spoken segment";
                        }
                    }
                }
            }
            catch (JsonException) { lock (gate) status = "Bubbles: waiting for a complete transcript update"; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException)
            { lock (gate) status = "Bubbles: " + e.Message; }
            if (Wait(100)) return;
        }
    }
    public void Dispose() { cancellation.Cancel(); view.Close(); }
}
