using YoChat.Core;

namespace YoChat.UI;

public sealed class SettingsForm : Form
{
    private readonly Button _userColor = new() { AutoSize = true, MaximumSize = new Size(0, 32) };
    private readonly Button _assistantColor = new() { AutoSize = true, MaximumSize = new Size(0, 32) };
    private static void SetColor(Button button, string value) {
        button.Tag = value; button.Text = "Choose...  " + value;
        button.BackColor = ColorTranslator.FromHtml(value);
        button.ForeColor = button.BackColor.GetBrightness() > .55f ? Color.Black : Color.White;
        button.UseVisualStyleBackColor = false;
    }
    private void ChooseColor(Button button) {
        using var picker = new ColorDialog { Color = button.BackColor, FullOpen = true };
        if (picker.ShowDialog(this) == DialogResult.OK)
            SetColor(button, $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}");
    }
    private readonly TextBox _wakePhrase = new();
    private readonly TextBox _hotkey = new();
    private readonly NumericUpDown _confidence = new();
    private readonly NumericUpDown _cooldown = new();
    private readonly Label _validation = new();
    private readonly CheckBox _bubbles = new() { Text = "Show live voice bubbles", AutoSize = true };
    private readonly TextBox _transcriptPath = new();

    public SettingsForm(AppSettings settings)
    {
        Text = "YoChat Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 560);
        Font = new Font("Segoe UI", 9F);

        _wakePhrase.Text = settings.WakePhrase;
        _bubbles.Checked = settings.BubblesEnabled;
        _transcriptPath.Text = settings.TranscriptPath;
        _transcriptPath.PlaceholderText = "Automatic (leave blank)";
        _hotkey.Text = settings.Hotkey;
        _confidence.DecimalPlaces = 2;
        _confidence.Increment = 0.01M;
        _confidence.Minimum = 0.50M;
        _confidence.Maximum = 1.00M;
        _confidence.Value = (decimal)settings.ConfidenceThreshold;
        _cooldown.Minimum = 1;
        _cooldown.Maximum = 30;
        _cooldown.Value = settings.CooldownSeconds;

        settings = settings.Normalized();
        SetColor(_userColor, settings.UserBubbleColor);
        SetColor(_assistantColor, settings.AssistantBubbleColor);
        _userColor.Click += (_, _) => ChooseColor(_userColor);
        _assistantColor.Click += (_, _) => ChooseColor(_assistantColor);
        BuildLayout();
    }

    public AppSettings? Result { get; private set; }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 12
        };
        for (int row = 0; row < 12; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "Wake phrase", _wakePhrase);
        AddRow(layout, 1, "Voice chat hotkey", _hotkey);
        AddRow(layout, 2, "Confidence", _confidence);
        AddRow(layout, 3, "Cooldown (seconds)", _cooldown);

        var hotkeyHelp = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(325, 0),
            Text = "Set this exact shortcut in ChatGPT Desktop → Settings → Voice → Voice chat hotkey. Example: Ctrl+Alt+Space.",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 2, 3, 12)
        };
        layout.Controls.Add(hotkeyHelp, 1, 4);

        var privacy = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(470, 0),
            Text = "Recognition uses a local Vosk wake model. YoChat does not save or upload microphone audio.",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 8, 3, 8)
        };
        AddRow(layout, 5, "Bubbles", _bubbles);
        AddRow(layout, 6, "Transcript file", _transcriptPath);
        AddRow(layout, 7, "Your message colour", _userColor);
        AddRow(layout, 8, "Reply colour", _assistantColor);
        layout.Controls.Add(privacy, 0, 9);
        layout.SetColumnSpan(privacy, 2);

        _validation.AutoSize = true;
        _validation.ForeColor = Color.Firebrick;
        _validation.Margin = new Padding(3, 2, 3, 8);
        layout.Controls.Add(_validation, 0, 10);
        layout.SetColumnSpan(_validation, 2);

        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += SaveClicked;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 11);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 3, 3, 10);
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        var phrase = WakePhraseGate.Normalize(_wakePhrase.Text);
        if (phrase.Length < 2)
        {
            _validation.Text = "Enter a wake phrase of at least two characters.";
            return;
        }

        if (!HotkeyParser.TryParse(_hotkey.Text, out var hotkey, out var error))
        {
            _validation.Text = error;
            return;
        }

        Result = new AppSettings
        {
            UserBubbleColor = (string)_userColor.Tag!,
            AssistantBubbleColor = (string)_assistantColor.Tag!,
            BubblesEnabled = _bubbles.Checked,
            TranscriptPath = _transcriptPath.Text.Trim(),
            WakePhrase = phrase,
            Hotkey = hotkey!.ToString(),
            ConfidenceThreshold = (float)_confidence.Value,
            CooldownSeconds = (int)_cooldown.Value
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
