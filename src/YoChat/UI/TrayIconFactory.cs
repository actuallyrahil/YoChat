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

        using var background = new SolidBrush(Color.FromArgb(74, 121, 235));
        using var status = new SolidBrush(color);
        using var white = new SolidBrush(Color.White);
        using var shape = new System.Drawing.Drawing2D.GraphicsPath();
        shape.AddArc(2, 3, 12, 12, 180, 90);
        shape.AddArc(18, 3, 12, 12, 270, 90);
        shape.AddArc(18, 15, 12, 12, 0, 90);
        shape.AddLine(14, 27, 6, 31);
        shape.AddLine(6, 31, 7, 25);
        shape.AddArc(2, 15, 12, 12, 90, 90);
        shape.CloseFigure();
        graphics.FillPath(background, shape);
        graphics.FillEllipse(white, 7, 13, 4, 4);
        graphics.FillEllipse(white, 14, 13, 4, 4);
        graphics.FillEllipse(white, 21, 13, 4, 4);
        graphics.FillEllipse(white, 21, 0, 11, 11);
        graphics.FillEllipse(status, 23, 2, 7, 7);

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

