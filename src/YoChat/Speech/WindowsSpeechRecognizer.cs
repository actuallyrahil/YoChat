using System.Speech.Recognition;

namespace YoChat.Speech;

public sealed class WindowsSpeechRecognizer : IWakePhraseRecognizer
{
    private readonly object _sync = new();
    private SpeechRecognitionEngine? _engine;

    public event EventHandler<WakePhraseDetectedEventArgs>? Detected;

    public event EventHandler<string>? StatusChanged;

    public bool IsListening
    {
        get
        {
            lock (_sync)
            {
                return _engine is not null;
            }
        }
    }

    public void Start(string wakePhrase)
    {
        lock (_sync)
        {
            StopCore();

            var engine = new SpeechRecognitionEngine();
            try
            {
                var grammarBuilder = new GrammarBuilder(wakePhrase)
                {
                    Culture = engine.RecognizerInfo.Culture
                };

                engine.LoadGrammar(new Grammar(grammarBuilder) { Name = "YoChat wake phrase" });
                engine.SpeechRecognized += OnSpeechRecognized;
                engine.RecognizeCompleted += OnRecognizeCompleted;
                engine.SetInputToDefaultAudioDevice();
                engine.RecognizeAsync(RecognizeMode.Multiple);
                _engine = engine;
            }
            catch
            {
                engine.SpeechRecognized -= OnSpeechRecognized;
                engine.RecognizeCompleted -= OnRecognizeCompleted;
                engine.Dispose();
                throw;
            }
        }

        StatusChanged?.Invoke(this, "Listening");
    }

    public void Stop()
    {
        lock (_sync)
        {
            StopCore();
        }

        StatusChanged?.Invoke(this, "Paused");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            StopCore();
        }
    }

    private void StopCore()
    {
        if (_engine is null)
        {
            return;
        }

        var engine = _engine;
        _engine = null;
        engine.SpeechRecognized -= OnSpeechRecognized;
        engine.RecognizeCompleted -= OnRecognizeCompleted;

        try
        {
            engine.RecognizeAsyncCancel();
        }
        catch (InvalidOperationException)
        {
            // The recognizer already stopped.
        }
        finally
        {
            engine.Dispose();
        }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        Detected?.Invoke(this, new WakePhraseDetectedEventArgs(e.Result.Text, e.Result.Confidence));
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (e.Error is not null)
        {
            StatusChanged?.Invoke(this, $"Recognition error: {e.Error.Message}");
        }
    }
}

