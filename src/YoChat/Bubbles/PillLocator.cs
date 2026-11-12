using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace YoChat;

// Reads only the existing floating voice control. Never activates or opens a window.
sealed class PillLocator
{
    delegate bool EnumCallback(IntPtr window, IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback, IntPtr context);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    IntPtr handle;
    uint owner;
    long searched, refreshed;
    AutomationElement? stop;
    readonly CacheRequest bounds = MakeCache(AutomationElement.BoundingRectangleProperty, AutomationElement.IsOffscreenProperty);

    static CacheRequest MakeCache(params AutomationProperty[] properties)
    {
        var cache = new CacheRequest();
        foreach (var property in properties) cache.Add(property);
        return cache;
    }

    public PillPosition Read()
    {
        long now = Environment.TickCount64;
        try
        {
            GetWindowThreadProcessId(handle, out var currentOwner);
            if (handle == IntPtr.Zero || owner != currentOwner)
            {
                handle = IntPtr.Zero; stop = null;
                if (now - searched < 1000) return new(false, 0, 0);
                searched = now;
                Find();
            }
            if (handle == IntPtr.Zero || !IsWindowVisible(handle)) { handle = IntPtr.Zero; stop = null; return new(false, 0, 0); }
            if (stop == null || now - refreshed > 1000)
            {
                stop = AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,
                    new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.NameProperty, "Stop voice chat")));
                refreshed = now;
            }
            if (stop == null) { handle = IntPtr.Zero; return new(false, 0, 0); }
            var cached = stop.GetUpdatedCache(bounds).Cached;
            var rect = cached.BoundingRectangle;
            return !cached.IsOffscreen && !rect.IsEmpty && rect.Width > 0
                ? new(true, rect.X + rect.Width / 2, rect.Top - 1) : new(false, 0, 0);
        }
        catch { stop = null; handle = IntPtr.Zero; return new(false, 0, 0); }
    }

    void Find()
    {
        var owners = new HashSet<uint>();
        foreach (var name in new[] { "ChatGPT", "Codex" })
            foreach (var process in Process.GetProcessesByName(name))
                using (process) owners.Add((uint)process.Id);
        var candidates = new List<(IntPtr Window, uint Owner)>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var pid);
            if (owners.Contains(pid) && IsWindowVisible(window)) candidates.Add((window, pid));
            return true;
        }, IntPtr.Zero);
        foreach (var candidate in candidates)
        {
            try
            {
                var root = AutomationElement.FromHandle(candidate.Window);
                // The full chat uses "Mute microphone". The floating pill uses this control.
                var mute = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Mute voice chat"));
                if (mute == null) continue;
                handle = candidate.Window; owner = candidate.Owner; return;
            }
            catch { }
        }
    }

    public Obstacle[] ReadObstacles()
    {
        try
        {
            var window = handle;
            GetWindowThreadProcessId(window, out var pid);
            if (window == IntPtr.Zero || pid != owner || !IsWindowVisible(window)) return [];
            var cache = MakeCache(AutomationElement.BoundingRectangleProperty, AutomationElement.IsOffscreenProperty, AutomationElement.ControlTypeProperty);
            AutomationElementCollection nodes;
            using (cache.Activate()) nodes = AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants, Condition.TrueCondition);
            var obstacles = new List<Obstacle>();
            foreach (AutomationElement node in nodes)
            {
                var value = node.Cached; var r = value.BoundingRectangle;
                if (value.IsOffscreen || r.IsEmpty || r.Width > 650 || r.Height > 400) continue;
                if (value.ControlType == ControlType.Text || value.ControlType == ControlType.Button || value.ControlType == ControlType.Group)
                    obstacles.Add(new(r.Left, r.Top, r.Right, r.Bottom));
            }
            return obstacles.ToArray();
        }
        catch { return []; }
    }
}
