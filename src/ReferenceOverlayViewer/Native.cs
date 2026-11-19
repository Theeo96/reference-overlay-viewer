using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ReferenceOverlayViewer;

internal static class Native
{
    internal const int ExLayered = 0x80000, ExTransparent = 0x20, ExNoActivate = 0x8000000;
    internal const int WmHotkey = 0x312, WmMouseActivate = 0x21, WmDisplayChange = 0x7e;
    internal const uint NoActivate = 0x10, NoZOrder = 0x4;
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public readonly int Width => Right - Left; public readonly int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern nint MonitorFromRect(ref Rect rect, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);

    internal static nint Handle(Window window) => new WindowInteropHelper(window).Handle;
    internal static int Style(nint hwnd) => (int)GetWindowLongPtr(hwnd, -20);
    internal static void SetInputPolicy(nint hwnd, bool edit)
    {
        var style = Style(hwnd) | ExLayered;
        style = edit ? style & ~(ExTransparent | ExNoActivate) : style | ExTransparent | ExNoActivate;
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(hwnd, -20, style);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        if (!SetWindowPos(hwnd, 0, 0, 0, 0, 0, NoActivate | NoZOrder | 0x1 | 0x2 | 0x20)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    internal static Rect AccessibleRect(Rect rect)
    {
        var monitor = MonitorFromRect(ref rect, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return rect;
        var width = Math.Min(rect.Width, info.Work.Width);
        var height = Math.Min(rect.Height, info.Work.Height);
        var x = Math.Clamp(rect.Left, info.Work.Left, info.Work.Right - width);
        var y = Math.Clamp(rect.Top, info.Work.Top, info.Work.Bottom - height);
        return new Rect { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }
}
