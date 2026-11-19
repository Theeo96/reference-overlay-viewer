using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ReferenceOverlayViewer;

internal enum Edges { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

internal sealed class OverlayWindow : Window
{
    internal ImageSurface Surface { get; } = new();
    private readonly ToolbarWindow _toolbar;
    private Point? _panPoint;
    internal bool Editing { get; private set; }
    internal OverlayWindow(ToolbarWindow toolbar)
    {
        _toolbar = toolbar;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Width = 760; Height = 460; Content = Surface;
        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(Native.Handle(this)).AddHook(Hook);
            SetEditing(false);
        };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(Surface.Refresh);
        Closing += (_, e) =>
        {
            // Alt+F4 on an active image window must close the whole viewer as one unit.
            if (_toolbar.IsClosing) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(_toolbar.Close);
        };
        Surface.MouseLeftButtonDown += Down;
        Surface.MouseMove += Move;
        Surface.MouseLeftButtonUp += (_, e) => { StopGesture(); e.Handled = true; };
        Surface.LostMouseCapture += (_, _) => { _panPoint = null; _toolbar.EndResize(); };
        Surface.MouseWheel += (_, e) =>
        {
            if (!Editing) return;
            Surface.Viewport.ZoomAt(Surface.Viewport.Zoom * Math.Pow(1.15, e.Delta / 120.0), e.GetPosition(Surface));
            Surface.InvalidateVisual(); _toolbar.UpdateZoom(); e.Handled = true;
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { _toolbar.SetEditing(false); e.Handled = true; } };
    }
    private nint Hook(nint hwnd, int message, nint w, nint l, ref bool handled)
    {
        if (message == Native.WmMouseActivate && !Editing) { handled = true; return 3; }
        return 0;
    }
    internal void SetEditing(bool edit)
    {
        StopGesture(); Editing = edit; Surface.Editing = edit;
        if (Native.Handle(this) != 0) Native.SetInputPolicy(Native.Handle(this), edit);
        Surface.Cursor = edit ? Cursors.Hand : Cursors.Arrow;
        Surface.InvalidateVisual();
    }
    internal void StopGesture()
    {
        _panPoint = null; _toolbar.EndResize();
        if (Surface.IsMouseCaptured) Surface.ReleaseMouseCapture();
    }
    private Edges HitEdges(Point point)
    {
        var edge = Edges.None;
        if (point.X < 7) edge |= Edges.Left;
        if (point.X > Surface.ActualWidth - 7) edge |= Edges.Right;
        if (point.Y < 7) edge |= Edges.Top;
        if (point.Y > Surface.ActualHeight - 7) edge |= Edges.Bottom;
        return edge;
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        if (!Editing) return;
        Activate();
        var point = e.GetPosition(Surface); var edge = HitEdges(point);
        if (edge == Edges.None) _panPoint = point;
        else _toolbar.BeginResize(edge);
        Surface.CaptureMouse(); e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (!Editing) return;
        if (_toolbar.Resizing) { _toolbar.UpdateResize(); return; }
        var point = e.GetPosition(Surface);
        if (_panPoint is { } previous && e.LeftButton == MouseButtonState.Pressed)
        {
            Surface.Viewport.Pan(point.X - previous.X, point.Y - previous.Y);
            _panPoint = point; Surface.InvalidateVisual(); return;
        }
        Surface.Cursor = HitEdges(point) switch
        {
            Edges.Left or Edges.Right => Cursors.SizeWE,
            Edges.Top or Edges.Bottom => Cursors.SizeNS,
            Edges.Left | Edges.Top or Edges.Right | Edges.Bottom => Cursors.SizeNWSE,
            Edges.Right | Edges.Top or Edges.Left | Edges.Bottom => Cursors.SizeNESW,
            _ => Cursors.Hand
        };
    }
}
