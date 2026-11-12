using System.ComponentModel;
using System.Runtime.InteropServices;

namespace YoChat.Core;

public sealed record Hotkey(Keys Key, bool Control, bool Alt, bool Shift, bool Windows)
{
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Windows) parts.Add("Win");
        parts.Add(HotkeyParser.FormatKey(Key));
        return string.Join('+', parts);
    }
}

public static class HotkeyParser
{
    public static bool TryParse(string? value, out Hotkey? hotkey, out string? error)
    {
        hotkey = null;
        error = null;

        var tokens = (value ?? string.Empty)
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 2)
        {
            error = "Use at least one modifier and one key, for example Ctrl+Alt+Space.";
            return false;
        }

        var control = false;
        var alt = false;
        var shift = false;
        var windows = false;
        Keys? key = null;

        foreach (var token in tokens)
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    control = true;
                    continue;
                case "ALT":
                    alt = true;
                    continue;
                case "SHIFT":
                    shift = true;
                    continue;
                case "WIN":
                case "WINDOWS":
                    windows = true;
                    continue;
            }

            if (key is not null)
            {
                error = "A hotkey can contain only one non-modifier key.";
                return false;
            }

            if (!TryParseKey(token, out var parsedKey))
            {
                error = $"“{token}” is not a supported key.";
                return false;
            }

            key = parsedKey;
        }

        if (!(control || alt || shift || windows))
        {
            error = "Include Ctrl, Alt, Shift, or Win so normal typing is not triggered.";
            return false;
        }

        if (key is null)
        {
            error = "Choose a non-modifier key.";
            return false;
        }

        hotkey = new Hotkey(key.Value, control, alt, shift, windows);
        return true;
    }

    internal static string FormatKey(Keys key) => key switch
    {
        Keys.Escape => "Esc",
        Keys.Space => "Space",
        Keys.Return => "Enter",
        >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(),
        _ => key.ToString()
    };

    private static bool TryParseKey(string token, out Keys key)
    {
        var alias = token.ToUpperInvariant() switch
        {
            "ESC" => nameof(Keys.Escape),
            "ENTER" => nameof(Keys.Return),
            "SPACEBAR" => nameof(Keys.Space),
            _ => token
        };

        if (alias.Length == 1 && char.IsDigit(alias[0]))
        {
            alias = $"D{alias}";
        }

        return Enum.TryParse(alias, true, out key) &&
               key is not (Keys.Control or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
                   Keys.Alt or Keys.Menu or Keys.LMenu or Keys.RMenu or
                   Keys.Shift or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
                   Keys.LWin or Keys.RWin or Keys.None);
    }
}

public interface IHotkeyDispatcher
{
    void Send(Hotkey hotkey);
}

public sealed class WindowsHotkeyDispatcher : IHotkeyDispatcher
{
    private const uint InputKeyboard = 1;
    private const uint ExtendedKey = 0x0001;
    private const uint KeyUp = 0x0002;

    public void Send(Hotkey hotkey)
    {
        var modifiers = new List<Keys>(4);
        if (hotkey.Control) modifiers.Add(Keys.ControlKey);
        if (hotkey.Alt) modifiers.Add(Keys.Menu);
        if (hotkey.Shift) modifiers.Add(Keys.ShiftKey);
        if (hotkey.Windows) modifiers.Add(Keys.LWin);

        var inputs = new List<Input>((modifiers.Count + 1) * 2);
        inputs.AddRange(modifiers.Select(key => CreateInput(key, false)));
        inputs.Add(CreateInput(hotkey.Key, false));
        inputs.Add(CreateInput(hotkey.Key, true));
        inputs.AddRange(modifiers.AsEnumerable().Reverse().Select(key => CreateInput(key, true)));

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        if (sent != inputs.Count)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not send the configured hotkey.");
        }
    }

    private static Input CreateInput(Keys key, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = (ushort)(key & Keys.KeyCode),
                Flags = (IsExtendedKey(key) ? ExtendedKey : 0) | (keyUp ? KeyUp : 0)
            }
        }
    };

    private static bool IsExtendedKey(Keys key) => key is
        Keys.Insert or Keys.Delete or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or
        Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.NumLock or Keys.Divide or
        Keys.PrintScreen or Keys.LWin or Keys.RWin;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        // INPUT's native union is sized by MOUSEINPUT, which is larger than
        // KEYBDINPUT on Windows. Keeping it here makes SendInput's cbSize valid.
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}

