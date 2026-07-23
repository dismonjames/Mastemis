using System.Runtime.InteropServices;
using System.Text;

namespace Mastemis.VisualReview;

internal sealed class X11WindowController : IDisposable
{
    private static readonly XErrorHandler ErrorHandler = (_, _) => 0;
    private readonly nint display;
    private readonly nint root;

    public X11WindowController()
    {
        display = XOpenDisplay(null);
        if (display == 0) throw new InvalidOperationException("X11 display is unavailable.");
        XSetErrorHandler(ErrorHandler);
        root = XDefaultRootWindow(display);
    }

    public IReadOnlySet<nint> MastemisWindowIds() => Enumerate(root)
        .Where(window => window.ClassName == "Mastemis.Client")
        .Select(window => window.Id)
        .ToHashSet();

    public WindowInfo? WaitForNewWindow(IReadOnlySet<nint> existingWindows, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var match = Enumerate(root).FirstOrDefault(window => window.ClassName == "Mastemis.Client" &&
                window.Title == "Mastemis" && !existingWindows.Contains(window.Id));
            if (match is not null) return match;
            Thread.Sleep(150);
        }
        return null;
    }

    public bool Activate(WindowInfo window)
    {
        XMapRaised(display, window.Id);
        var active = XInternAtom(display, "_NET_ACTIVE_WINDOW", false);
        var message = new XEvent { ClientMessage = new XClientMessageEvent { type = 33, display = display, window = window.Id, message_type = active, format = 32, data0 = 2 } };
        XSendEvent(display, root, false, 1L << 19 | 1L << 20, ref message);
        XFlush(display);
        Thread.Sleep(300);
        return GetCardinal(root, active) == (ulong)window.Id;
    }

    public WindowInfo Refresh(WindowInfo window)
    {
        if (XGetWindowAttributes(display, window.Id, out var attributes) == 0) return window;
        XTranslateCoordinates(display, window.Id, root, 0, 0, out var x, out var y, out _);
        return window with { X = x, Y = y, Width = attributes.width, Height = attributes.height };
    }

    public bool SendKeyboardSmoke(WindowInfo window)
    {
        if (!Activate(window)) return false;
        foreach (var key in new[] { "Tab", "Tab", "Return", "Escape", "Left", "Right" })
        {
            var code = XKeysymToKeycode(display, XStringToKeysym(key));
            if (code == 0) return false;
            XTestFakeKeyEvent(display, code, true, 0); XTestFakeKeyEvent(display, code, false, 0); XFlush(display); Thread.Sleep(80);
        }
        return true;
    }

    private IEnumerable<WindowInfo> Enumerate(nint parent)
    {
        if (XQueryTree(display, parent, out _, out _, out var children, out var count) == 0) yield break;
        try
        {
            for (var i = 0u; i < count; i++)
            {
                var id = Marshal.ReadIntPtr(children, checked((int)i * IntPtr.Size));
                var info = Read(id);
                if (info is not null) yield return info;
                foreach (var child in Enumerate(id)) yield return child;
            }
        }
        finally { if (children != 0) XFree(children); }
    }

    private WindowInfo? Read(nint id)
    {
        var pid = (int)GetCardinal(id, XInternAtom(display, "_NET_WM_PID", false));
        if (XGetClassHint(display, id, out var hint) == 0) return null;
        try
        {
            var className = Marshal.PtrToStringAnsi(hint.res_class) ?? string.Empty;
            var title = ReadText(id, XInternAtom(display, "_NET_WM_NAME", false));
            if (title.Length == 0 && XFetchName(display, id, out var titlePointer) != 0 && titlePointer != 0)
            {
                try { title = Marshal.PtrToStringAnsi(titlePointer) ?? string.Empty; }
                finally { XFree(titlePointer); }
            }
            if (XGetWindowAttributes(display, id, out var attributes) == 0) return null;
            XTranslateCoordinates(display, id, root, 0, 0, out var x, out var y, out _);
            return new(id, pid, title, className, x, y, attributes.width, attributes.height);
        }
        finally { if (hint.res_name != 0) XFree(hint.res_name); if (hint.res_class != 0) XFree(hint.res_class); }
    }

    private string ReadText(nint window, nint atom)
    {
        if (XGetWindowProperty(display, window, atom, 0, 1024, false, 0, out _, out _, out var count, out _, out var data) != 0 || data == 0) return string.Empty;
        try { var bytes = new byte[(int)count]; Marshal.Copy(data, bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
        finally { XFree(data); }
    }

    private ulong GetCardinal(nint window, nint atom)
    {
        if (XGetWindowProperty(display, window, atom, 0, 1, false, 0, out _, out _, out var count, out _, out var data) != 0 || data == 0 || count == 0) return 0;
        try { return (ulong)Marshal.ReadIntPtr(data); } finally { XFree(data); }
    }

    public void Dispose() { if (display != 0) XCloseDisplay(display); }

    internal sealed record WindowInfo(nint Id, int ProcessId, string Title, string ClassName, int X, int Y, int Width, int Height);
    [StructLayout(LayoutKind.Sequential)] private struct XClassHint { public nint res_name; public nint res_class; }
    [StructLayout(LayoutKind.Sequential)] private struct XWindowAttributes { public int x, y, width, height, border_width, depth; public nint visual, root; public int @class, bit_gravity, win_gravity, backing_store; public ulong backing_planes, backing_pixel; public int save_under; public nint colormap; public int map_installed, map_state; public long all_event_masks, your_event_mask, do_not_propagate_mask; public int override_redirect; public nint screen; }
    [StructLayout(LayoutKind.Explicit, Size = 192)] private struct XEvent { [FieldOffset(0)] public XClientMessageEvent ClientMessage; }
    [StructLayout(LayoutKind.Sequential)] private struct XClientMessageEvent { public int type; public ulong serial; public int send_event; public nint display, window, message_type; public int format; public nint data0, data1, data2, data3, data4; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int XErrorHandler(nint display, nint errorEvent);

    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? name);
    [DllImport("libX11.so.6")] private static extern nint XSetErrorHandler(XErrorHandler handler);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern nint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern int XQueryTree(nint display, nint window, out nint root, out nint parent, out nint children, out uint count);
    [DllImport("libX11.so.6")] private static extern int XGetClassHint(nint display, nint window, out XClassHint hint);
    [DllImport("libX11.so.6")] private static extern int XFetchName(nint display, nint window, out nint name);
    [DllImport("libX11.so.6")] private static extern int XGetWindowAttributes(nint display, nint window, out XWindowAttributes attributes);
    [DllImport("libX11.so.6")] private static extern int XTranslateCoordinates(nint display, nint source, nint destination, int sourceX, int sourceY, out int destinationX, out int destinationY, out nint child);
    [DllImport("libX11.so.6")] private static extern int XGetWindowProperty(nint display, nint window, nint property, long offset, long length, bool delete, nint requestedType, out nint actualType, out int format, out ulong count, out ulong remaining, out nint data);
    [DllImport("libX11.so.6")] private static extern nint XInternAtom(nint display, string name, bool onlyIfExists);
    [DllImport("libX11.so.6")] private static extern int XMapRaised(nint display, nint window);
    [DllImport("libX11.so.6")] private static extern int XSendEvent(nint display, nint window, bool propagate, long mask, ref XEvent value);
    [DllImport("libX11.so.6")] private static extern int XFlush(nint display);
    [DllImport("libX11.so.6")] private static extern int XFree(nint data);
    [DllImport("libX11.so.6")] private static extern nint XStringToKeysym(string value);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display, nint keysym);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(nint display, uint keycode, bool down, ulong delay);
}
