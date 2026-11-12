namespace YoChat.Core;

public sealed class AppSettings
{
    public string UserBubbleColor { get; set; } = "#4A79EB";
    public string AssistantBubbleColor { get; set; } = "#303035";
    public static string NormalizeColor(string? value, string fallback) =>
        value is not null && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value.ToUpperInvariant() : fallback;
    public const string DefaultWakePhrase = "Yo Chat";
    public bool BubblesEnabled { get; set; } = true;
    public string TranscriptPath { get; set; } = "";
    public const string DefaultHotkey = "Ctrl+Alt+Space";

    public string WakePhrase { get; set; } = DefaultWakePhrase;

    public string Hotkey { get; set; } = DefaultHotkey;

    public float ConfidenceThreshold { get; set; } = 0.78f;

    public int CooldownSeconds { get; set; } = 4;

    public AppSettings Normalized()
    {
        var hotkey = HotkeyParser.TryParse(Hotkey, out var parsed, out _)
            ? parsed!.ToString()
            : DefaultHotkey;

        return new AppSettings
        {
            UserBubbleColor = NormalizeColor(UserBubbleColor, "#4A79EB"),
            AssistantBubbleColor = NormalizeColor(AssistantBubbleColor, "#303035"),
            BubblesEnabled = BubblesEnabled,
            TranscriptPath = TranscriptPath?.Trim() ?? "",
            WakePhrase = WakePhraseGate.Normalize(WakePhrase) is { Length: > 0 } phrase
                ? phrase
                : DefaultWakePhrase,
            Hotkey = hotkey,
            ConfidenceThreshold = Math.Clamp(ConfidenceThreshold, 0.5f, 1.0f),
            CooldownSeconds = Math.Clamp(CooldownSeconds, 1, 30)
        };
    }

    public AppSettings Copy() => new()
    {
        UserBubbleColor = UserBubbleColor,
        AssistantBubbleColor = AssistantBubbleColor,
        BubblesEnabled = BubblesEnabled,
        TranscriptPath = TranscriptPath,
        WakePhrase = WakePhrase,
        Hotkey = Hotkey,
        ConfidenceThreshold = ConfidenceThreshold,
        CooldownSeconds = CooldownSeconds
    };
}

