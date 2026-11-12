namespace YoChat;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            try
            {
                ApplicationConfiguration.Initialize();
                ContinuityReader.Test();
                MotionTests.Run();
                BubbleView.Test();
                using var settings = new UI.SettingsForm(new Core.AppSettings());
                settings.CreateControl();
                using var model = new Vosk.Model(Path.Combine(AppContext.BaseDirectory, "models", "vosk-model-small-en-us-0.15"));
                using var decoder = new Vosk.VoskRecognizer(model, 16000, Speech.WindowsSpeechRecognizer.CreateListeningGrammar("Yo Chat"));
                decoder.SetWords(true);
                decoder.AcceptWaveform(new byte[32000], 32000);
                if (Speech.WindowsSpeechRecognizer.ParseResult(decoder.FinalResult()) is not null)
                    throw new InvalidOperationException("Silence produced a wake candidate.");
                Console.WriteLine("PASS: packaged UI initializes without microphone access or ChatGPT navigation");
            }
            catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
            return;
        }
        using var instance = new Mutex(true, @"Local\YoChat.Tray", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        if (System.Diagnostics.Process.GetProcessesByName("OkayCodex").Length > 0)
        {
            MessageBox.Show("Exit OkayCodex from its tray menu, then start YoChat. Both apps would otherwise send the same voice shortcut.", "YoChat");
            return;
        }
        try { Application.Run(new TrayApplicationContext()); }
        catch (Exception error) { MessageBox.Show(error.Message, "YoChat could not start", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
