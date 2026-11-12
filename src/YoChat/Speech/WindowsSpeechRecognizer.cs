using System.Text.Json;
using NAudio.Wave;
using Vosk;
using YoChat.Core;

namespace YoChat.Speech;

// Local speech decoding with an explicit unknown-speech alternative.
// The previous SAPI grammar contained only the wake phrase and forced unrelated
// audio into it. Unknown tokens and extra words never pass WakePhraseGate.
public sealed class WindowsSpeechRecognizer : IWakePhraseRecognizer
{
    private readonly object _sync = new();
    private WaveInEvent? _capture;
    private Model? _model;
    private VoskRecognizer? _decoder;
    public event EventHandler<WakePhraseDetectedEventArgs>? Detected;
    public event EventHandler<string>? StatusChanged;
    public bool IsListening { get { lock (_sync) return _capture is not null; } }

    internal static string CreateListeningGrammar(string phrase) =>
        JsonSerializer.Serialize(new[] { WakePhraseGate.Normalize(phrase).ToLowerInvariant(), "[unk]" });

    internal static WakePhraseDetectedEventArgs? ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("text", out var textNode)) return null;
        var text = textNode.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Contains("[unk]", StringComparison.Ordinal)) return null;
        if (!doc.RootElement.TryGetProperty("result", out var words) || words.GetArrayLength() == 0) return null;
        var confidence = 1f;
        foreach (var word in words.EnumerateArray())
        {
            if (!word.TryGetProperty("conf", out var value) || !value.TryGetSingle(out var score) ||
                !float.IsFinite(score) || score < 0 || score > 1) return null;
            confidence = Math.Min(confidence, score);
        }
        return new(text, confidence);
    }

    public void Start(string wakePhrase)
    {
        Stop();
        var path = Path.Combine(AppContext.BaseDirectory, "models", "vosk-model-small-en-us-0.15");
        if (!File.Exists(Path.Combine(path, "am", "final.mdl")))
            throw new InvalidOperationException("Wake model missing. Use the complete portable folder or rerun the installer.");
        Model? model = null;
        VoskRecognizer? decoder = null;
        WaveInEvent? capture = null;
        try
        {
            Vosk.Vosk.SetLogLevel(-1);
            model = new Model(path);
            var normalized = WakePhraseGate.Normalize(wakePhrase).ToLowerInvariant();
            if (normalized.Length == 0) throw new InvalidOperationException("Choose a non-empty wake phrase.");
            foreach (var word in normalized.Split(' '))
                if (model.FindWord(word) < 0)
                    throw new InvalidOperationException($"The English wake model does not contain '{word}'. Choose another wake phrase.");
            decoder = new VoskRecognizer(model, 16000, CreateListeningGrammar(normalized));
            decoder.SetWords(true);
            capture = new WaveInEvent { DeviceNumber = -1, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
            capture.DataAvailable += OnAudio;
            capture.RecordingStopped += OnRecordingStopped;
            lock (_sync) { _model = model; _decoder = decoder; _capture = capture; }
            capture.StartRecording();
        }
        catch
        {
            lock (_sync) { _capture = null; _decoder = null; _model = null; }
            if (capture is not null)
            {
                capture.DataAvailable -= OnAudio;
                capture.RecordingStopped -= OnRecordingStopped;
                capture.Dispose();
            }
            decoder?.Dispose(); model?.Dispose();
            throw;
        }
        StatusChanged?.Invoke(this, "Listening");
    }

    private void OnAudio(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(sender, _capture) || _decoder is null) return;
            try
            {
                if (_decoder.AcceptWaveform(e.Buffer, e.BytesRecorded))
                {
                    var result = ParseResult(_decoder.Result());
                    if (result is not null) Detected?.Invoke(this, result);
                }
            }
            catch (Exception error)
            {
                StatusChanged?.Invoke(this, $"Recognition error: {error.Message}");
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            StatusChanged?.Invoke(this, $"Recognition error: {e.Exception.Message}");
    }

    public void Stop()
    {
        WaveInEvent? capture; VoskRecognizer? decoder; Model? model;
        lock (_sync)
        {
            capture = _capture; decoder = _decoder; model = _model;
            _capture = null; _decoder = null; _model = null;
        }
        // Do not hold the callback lock while waiting for audio capture to stop.
        if (capture is not null)
        {
            capture.DataAvailable -= OnAudio;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.StopRecording(); capture.Dispose();
        }
        decoder?.Dispose(); model?.Dispose();
        StatusChanged?.Invoke(this, "Paused");
    }
    public void Dispose() => Stop();
}
