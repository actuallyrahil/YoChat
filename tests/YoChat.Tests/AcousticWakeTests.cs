using NAudio.Wave;
using Vosk;
using YoChat.Core;
using YoChat.Speech;

static class AcousticWakeTests
{
    // Generated speech only; no microphone, network or hotkey calls.
    public static void Run(string modelPath, string fixtures)
    {
        Vosk.Vosk.SetLogLevel(-1);
        using var model = new Model(modelPath);
        for (int i = 0; i < 9; i++)
        {
            using var audio = new WaveFileReader(Path.Combine(fixtures, $"{i}.wav"));
            if (audio.WaveFormat.Channels != 1 || audio.WaveFormat.BitsPerSample != 16)
                throw new Exception("Expected mono PCM fixture.");
            using var decoder = new VoskRecognizer(model, audio.WaveFormat.SampleRate,
                WindowsSpeechRecognizer.CreateListeningGrammar("Yo Chat"));
            decoder.SetWords(true);
            var gate = new WakePhraseGate("Yo Chat", .78f, TimeSpan.Zero);
            int accepted = 0;
            void Inspect(string json)
            {
                var result = WindowsSpeechRecognizer.ParseResult(json);
                if (result is not null && gate.TryAccept(result.Text, result.Confidence, DateTimeOffset.UtcNow)) accepted++;
            }
            var buffer = new byte[4000];
            int count;
            while ((count = audio.Read(buffer, 0, buffer.Length)) > 0)
                if (decoder.AcceptWaveform(buffer, count)) Inspect(decoder.Result());
            Inspect(decoder.FinalResult());
            int expected = i == 6 ? 1 : 0;
            if (accepted != expected) throw new Exception($"Audio fixture {i}: expected {expected} wakes, got {accepted}.");
        }
        Console.WriteLine("PASS: 9 synthetic audio samples; only the complete Yo Chat phrase activates.");
    }
}
