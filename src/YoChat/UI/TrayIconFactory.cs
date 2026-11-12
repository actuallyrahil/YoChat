using System.Runtime.InteropServices;

namespace YoChat.UI;

internal static class TrayIconFactory
{
    public static Icon Create(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var background = new SolidBrush(Color.FromArgb(34, 38, 46));
        using var status = new SolidBrush(color);
        using var pen = new Pen(Color.White, 2.2f);

        graphics.FillEllipse(background, 1, 1, 30, 30);
        graphics.DrawArc(pen, 8, 6, 16, 18, 210, 300);
        graphics.DrawLine(pen, 16, 23, 16, 27);
        graphics.DrawLine(pen, 11, 27, 21, 27);
        graphics.FillEllipse(status, 22, 3, 8, 8);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}

