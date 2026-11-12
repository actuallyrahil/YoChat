using YoChat.Audio;
using YoChat.Core;

if (args.Contains("--capture-status", StringComparer.OrdinalIgnoreCase))
{
    var status = new WindowsChatGptCaptureDetector().GetStatus();
    Console.WriteLine($"ChatGPT capture state: {status.State}");
    if (status.Detail is { Length: > 0 })
    {
        Console.WriteLine(status.Detail);
    }

    return status.State == ChatGptCaptureState.Unavailable ? 1 : 0;
}

if (args.Length == 3 && args[0] == "--audio-regression")
{
    AcousticWakeTests.Run(args[1], args[2]);
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("Wake phrase normalization", WakePhraseNormalization),
    ("Wake grammar rejects unknown speech", WakeRecognitionRegressionTests.GrammarAllowsUnrelatedSpeech),
    ("Exact wake utterance and finite confidence", WakeRecognitionRegressionTests.ExactUtteranceOnly),
    ("Confidence threshold", ConfidenceThreshold),
    ("Cooldown debounce", CooldownDebounce),
    ("Hotkey parsing and canonical format", HotkeyParsing),
    ("Hotkey validation", HotkeyValidation),
    ("Settings normalization", SettingsNormalization),
    ("Custom colour persistence and validation", ColourSettings),
    ("Handoff when ChatGPT is idle", HandoffWhenChatGptIsIdle),
    ("Suppress handoff while ChatGPT captures", SuppressHandoffWhileChatGptCaptures),
    ("Suppress handoff when capture state is unknown", SuppressHandoffWhenStateIsUnknown),
    ("Direct transcript and repeated calls", ContinuityReader.Test),
    ("Duplicate transcript regression", TranscriptRegressionTests.Run),
    ("Pill motion and prompt clearance", MotionTests.Run),
    ("Transcript parser", LiveModel.Test),
    ("YoChat defaults and settings round trip", YoChatSettings),
    ("Transcript path configuration", TranscriptPaths)
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void WakePhraseNormalization()
{
    var gate = new WakePhraseGate("Okay Codex", 0.75f, TimeSpan.FromSeconds(3));
    Assert(gate.TryAccept("  okay   CODEX ", 0.90f, DateTimeOffset.UnixEpoch), "Equivalent whitespace/case should match.");
    Assert(!gate.TryAccept("Okay code", 0.99f, DateTimeOffset.UnixEpoch.AddSeconds(10)), "Different text must not match.");
}

static void ConfidenceThreshold()
{
    var gate = new WakePhraseGate("Okay Codex", 0.80f, TimeSpan.Zero);
    Assert(!gate.TryAccept("Okay Codex", 0.79f, DateTimeOffset.UnixEpoch), "Low confidence should be rejected.");
    Assert(gate.TryAccept("Okay Codex", 0.80f, DateTimeOffset.UnixEpoch), "Threshold confidence should pass.");
}

static void CooldownDebounce()
{
    var gate = new WakePhraseGate("Okay Codex", 0.70f, TimeSpan.FromSeconds(4));
    var start = DateTimeOffset.UnixEpoch;
    Assert(gate.TryAccept("Okay Codex", 0.90f, start), "First detection should pass.");
    Assert(!gate.TryAccept("Okay Codex", 0.90f, start.AddSeconds(3.9)), "Detection during cooldown should fail.");
    Assert(gate.TryAccept("Okay Codex", 0.90f, start.AddSeconds(4)), "Detection at cooldown boundary should pass.");
}

static void HotkeyParsing()
{
    Assert(HotkeyParser.TryParse("shift + ctrl + f10", out var hotkey, out _), "Valid hotkey did not parse.");
    Assert(hotkey!.ToString() == "Ctrl+Shift+F10", $"Unexpected canonical form: {hotkey}");
    Assert(HotkeyParser.TryParse("Win+Alt+1", out hotkey, out _), "Digit hotkey did not parse.");
    Assert(hotkey!.ToString() == "Alt+Win+1", $"Unexpected digit canonical form: {hotkey}");
}

static void HotkeyValidation()
{
    Assert(!HotkeyParser.TryParse("Space", out _, out _), "Unmodified key should be rejected.");
    Assert(!HotkeyParser.TryParse("Ctrl+Alt", out _, out _), "Modifier-only hotkey should be rejected.");
    Assert(!HotkeyParser.TryParse("Ctrl+A+B", out _, out _), "Multiple keys should be rejected.");
}

static void SettingsNormalization()
{
    var settings = new AppSettings
    {
        WakePhrase = "  Hello   Codex ",
        Hotkey = "shift+ctrl+f2",
        ConfidenceThreshold = 4,
        CooldownSeconds = 0
    }.Normalized();

    Assert(settings.WakePhrase == "Hello Codex", "Wake phrase was not normalized.");
    Assert(settings.Hotkey == "Ctrl+Shift+F2", "Hotkey was not normalized.");
    Assert(settings.ConfidenceThreshold == 1.0f, "Confidence was not clamped.");
    Assert(settings.CooldownSeconds == 1, "Cooldown was not clamped.");
}

static void HandoffWhenChatGptIsIdle()
{
    var decision = WakeHandoffPolicy.Decide(
        new ChatGptCaptureStatus(ChatGptCaptureState.NotCapturing));
    Assert(decision == WakeHandoffDecision.SendHotkey, "Idle ChatGPT should allow the hotkey.");
}

static void SuppressHandoffWhileChatGptCaptures()
{
    var decision = WakeHandoffPolicy.Decide(
        new ChatGptCaptureStatus(ChatGptCaptureState.Capturing));
    Assert(
        decision == WakeHandoffDecision.SuppressWhileChatGptCaptures,
        "Active ChatGPT capture should suppress the hotkey.");
}

static void SuppressHandoffWhenStateIsUnknown()
{
    var decision = WakeHandoffPolicy.Decide(
        new ChatGptCaptureStatus(ChatGptCaptureState.Unavailable, "test failure"));
    Assert(
        decision == WakeHandoffDecision.SuppressBecauseStateIsUnknown,
        "Unknown state should fail closed instead of risking a voice-session toggle.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void YoChatSettings()
{
    var defaults = new AppSettings();
    Assert(defaults.WakePhrase == "Yo Chat" && defaults.BubblesEnabled, "New installs must enable the requested phrase and bubbles.");
    var path = Path.Combine(AppContext.BaseDirectory, "yochat-test-" + Guid.NewGuid().ToString("N"), "settings.json");
    try
    {
        var store = new YoChat.Infrastructure.SettingsStore(path);
        var settings = new AppSettings { BubblesEnabled = false, TranscriptPath = "custom.json", WakePhrase = "Hey Chat" };
        store.Save(settings);
        var loaded = store.Load();
        Assert(!loaded.BubblesEnabled && loaded.TranscriptPath == "custom.json" && loaded.WakePhrase == WakePhraseGate.Normalize("Hey Chat"), "Settings must survive restart.");
        Assert(loaded.Copy().TranscriptPath == loaded.TranscriptPath && !loaded.Copy().BubblesEnabled, "Copy must preserve bubble configuration.");
    }
    finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!); }
}

static void TranscriptPaths()
{
    var configured = Path.Combine(Path.GetTempPath(), "custom-voice.json");
    Assert(YoChat.BubbleEngine.ResolveTranscriptPath(configured) == Path.GetFullPath(configured), "Explicit source must take precedence.");
    Assert(Path.GetFileName(YoChat.BubbleEngine.ResolveTranscriptPath("")) == "realtime-voice-continuity.json", "Default source must be the direct transcript.");
}

static void ColourSettings() {
    var config = new AppSettings { UserBubbleColor="#abcdef", AssistantBubbleColor="#FFFFFF" }.Normalized();
    var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(config.Copy()))!;
    Assert(restored.UserBubbleColor=="#ABCDEF" && restored.AssistantBubbleColor=="#FFFFFF", "Custom colours must survive copying and persistence.");
    Assert(new AppSettings { UserBubbleColor="invalid", AssistantBubbleColor=null! }.Normalized().UserBubbleColor=="#4A79EB", "Invalid colours must fall back safely.");
    Assert(new AppSettings { AssistantBubbleColor=null! }.Normalized().AssistantBubbleColor=="#303035", "Null colour must fall back safely.");
}
