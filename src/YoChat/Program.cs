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
