namespace YoChat.Speech;

public sealed record WakePhraseDetectedEventArgs(string Text, float Confidence);

public interface IWakePhraseRecognizer : IDisposable
{
    event EventHandler<WakePhraseDetectedEventArgs>? Detected;

    event EventHandler<string>? StatusChanged;

    bool IsListening { get; }

    void Start(string wakePhrase);

    void Stop();
}

