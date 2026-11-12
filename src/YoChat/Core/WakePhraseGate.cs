namespace YoChat.Core;

public sealed class WakePhraseGate
{
    private readonly object _sync = new();
    private DateTimeOffset? _lastAcceptedAt;

    public WakePhraseGate(string wakePhrase, float confidenceThreshold, TimeSpan cooldown)
    {
        WakePhrase = Normalize(wakePhrase);
        ConfidenceThreshold = confidenceThreshold;
        Cooldown = cooldown;
    }

    public string WakePhrase { get; }

    public float ConfidenceThreshold { get; }

    public TimeSpan Cooldown { get; }

    public bool TryAccept(string recognizedText, float confidence, DateTimeOffset now)
    {
        if (WakePhrase.Length == 0 || !float.IsFinite(confidence) || confidence < 0 || confidence > 1 ||
            !float.IsFinite(ConfidenceThreshold) ||
            !string.Equals(Normalize(recognizedText), WakePhrase, StringComparison.OrdinalIgnoreCase) ||
            confidence < ConfidenceThreshold)
        {
            return false;
        }

        lock (_sync)
        {
            if (_lastAcceptedAt is not null && now - _lastAcceptedAt < Cooldown)
            {
                return false;
            }

            _lastAcceptedAt = now;
            return true;
        }
    }

    public static string Normalize(string? value) =>
        string.Join(' ', new string((value ?? string.Empty).Select(c => char.IsPunctuation(c) ? ' ' : c).ToArray())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
