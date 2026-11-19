using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ReferenceOverlayViewer;

public partial class ToolbarWindow : Window
{
    private readonly Settings _settings;
    private readonly string _settingsPath;
    private readonly OverlayWindow _overlay;
    private bool _ready, _syncing, _closing, _updatingControls, _hidden;
    private double _bodyHeight;
    private string? _imagePath;
    private int _loadGeneration;
    private Edges _resizeEdges;
    private Native.Point _resizeStart;
    private Native.Rect _resizeRect, _lastRect;
    private double _resizeDpi;
    private readonly List<int> _hotkeys = [];
    private string _hideKey = "등록 실패", _editKey = "등록 실패";
    public bool Editing { get; private set; }
    internal bool Resizing => _resizeEdges != Edges.None;
    internal bool IsClosing => _closing;
    internal OverlayWindow Overlay => _overlay;

    public ToolbarWindow() : this(Settings.Load(Settings.DefaultPath), Settings.DefaultPath, true) { }

    internal ToolbarWindow(Settings settings, string settingsPath, bool registerHotkeys)
    {
        _settings = settings; _settingsPath = settingsPath;
        InitializeComponent();
        _bodyHeight = _settings.BodyHeight; Width = _settings.Width;
        _overlay = new OverlayWindow(this);
        _overlay.Surface.ContentOpacity = _settings.Opacity;
        OpacitySlider.Value = _settings.Opacity * 100;
        _overlay.Surface.ViewChanged += UpdateZoom;
        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(Native.Handle(this)).AddHook(Hook);
            if (registerHotkeys)
            {
                _hideKey = Register(1, 0x52, "Ctrl+Alt+R", 0x7B, "Ctrl+Shift+F12");
                _editKey = Register(2, 0x45, "Ctrl+Alt+E", 0x7A, "Ctrl+Shift+F11");
            }
        };
        Loaded += (_, _) => InitializeWindows();
        LocationChanged += (_, _) => SyncBody();
        SizeChanged += (_, _) => SyncBody();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(() => { SyncBody(); _overlay.Surface.Refresh(); });
        StateChanged += (_, _) =>
        {
            if (!_ready) return;
            if (WindowState == WindowState.Minimized) { _overlay.StopGesture(); _overlay.Hide(); }
            else { WindowState = WindowState.Normal; ShowBody(); SyncBody(); }
        };
        PreviewMouseLeftButtonDown += ToolbarResizeDown;
        MouseMove += (_, _) => { if (Resizing) UpdateResize(); };
        MouseLeftButtonUp += (_, _) => { EndResize(); if (IsMouseCaptured) ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => EndResize();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Editing) { SetEditing(false); e.Handled = true; }
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { OpenFile(); e.Handled = true; }
        };
        Closing += OnClosing;
    }

    private void InitializeWindows()
    {
        _overlay.Owner = this;
        _ready = true;
        Native.GetWindowRect(Native.Handle(this), out var rect);
        var dpi = Native.GetDpiForWindow(Native.Handle(this)) / 96.0;
        var x = _settings.ScreenX ?? rect.Left; var y = _settings.ScreenY ?? rect.Top;
        var group = Native.AccessibleRect(new Native.Rect { Left = x, Top = y, Right = x + rect.Width, Bottom = y + rect.Height + (int)(_bodyHeight * dpi) });
        _bodyHeight = Math.Max(120, group.Height / dpi - Height);
        Native.SetWindowPos(Native.Handle(this), 0, group.Left, group.Top, group.Width, rect.Height, Native.NoActivate | Native.NoZOrder);
        ShowBody(); SyncBody(); SetEditing(false);
        if (_settings.LastImage is { } path && File.Exists(path)) LoadImage(path, _settings.Mode);
        if (_hotkeys.Count < 2) StatusLabel.Text = "일부 단축키 등록 실패 · 도움말에서 확인하세요";
    }

    private void ShowBody()
    {
        if (!_hidden && WindowState == WindowState.Normal && !_closing && !_overlay.IsVisible)
        {
            _overlay.ShowActivated = false;
            _overlay.Show();
        }
    }

    private void SyncBody()
    {
        if (!_ready || _syncing || _closing || WindowState != WindowState.Normal || _hidden) return;
        _syncing = true;
        try
        {
            Native.GetWindowRect(Native.Handle(this), out var rect);
            _lastRect = rect;
            if (Native.Handle(_overlay) == 0) return;
            var dpi = Native.GetDpiForWindow(Native.Handle(this)) / 96.0;
            // Use physical HWND edges to avoid DIP rounding seams between two windows.
            Native.SetWindowPos(Native.Handle(_overlay), 0, rect.Left, rect.Bottom, rect.Width,
                Math.Max(1, (int)Math.Round(_bodyHeight * dpi)), Native.NoActivate | Native.NoZOrder);
        }
        finally { _syncing = false; }
    }

    private string Register(int id, uint key, string label, uint fallbackKey, string fallbackLabel)
    {
        if (Native.RegisterHotKey(Native.Handle(this), id, 0x4003, key)) { _hotkeys.Add(id); return label; }
        if (Native.RegisterHotKey(Native.Handle(this), id, 0x4006, fallbackKey)) { _hotkeys.Add(id); return fallbackLabel; }
        return "등록 실패 (다른 앱이 사용 중)";
    }

    private nint Hook(nint hwnd, int message, nint w, nint l, ref bool handled)
    {
        if (message == Native.WmHotkey)
        {
            if (w == 1) ToggleHidden();
            if (w == 2 && !_hidden && WindowState == WindowState.Normal) SetEditing(!Editing);
            handled = true;
        }
        if (message == Native.WmDisplayChange)
            Dispatcher.BeginInvoke(() =>
            {
                if (!_ready || _hidden || WindowState != WindowState.Normal) return;
                Native.GetWindowRect(hwnd, out var rect);
                var dpi = Native.GetDpiForWindow(hwnd) / 96.0;
                rect.Bottom += (int)(_bodyHeight * dpi);
                var safe = Native.AccessibleRect(rect);
                _bodyHeight = Math.Max(120, safe.Height / dpi - Height);
                Native.SetWindowPos(hwnd, 0, safe.Left, safe.Top, safe.Width, (int)(Height * dpi), Native.NoActivate | Native.NoZOrder);
                SyncBody();
            });
        return 0;
    }

    private void ToggleHidden()
    {
        _overlay.StopGesture();
        if (!_hidden)
        {
            _hidden = true; _overlay.Hide(); Hide();
        }
        else
        {
            _hidden = false; ShowActivated = false;
            Show(); WindowState = WindowState.Normal; ShowBody(); SyncBody();
        }
    }

    internal void SetEditing(bool edit)
    {
        Editing = edit; EndResize();
        if (IsMouseCaptured) ReleaseMouseCapture();
        _overlay.SetEditing(edit);
        EditToggle.IsChecked = edit;
        EditToggle.Content = edit ? "편집 모드 ON" : "편집 모드 OFF";
        EditControls.IsEnabled = edit;
        UpdateStatus();
    }

    private void UpdateStatus(string? message = null)
    {
        StatusLabel.Text = message ?? $"{(_imagePath is null ? "이미지 없음" : Path.GetFileName(_imagePath))} · {(Editing ? "드래그: 이미지 이동 / 테두리: 크기 / Esc: 잠금" : "본문 클릭 통과 / 상단 드래그: 창 이동")}";
    }

    public async void LoadImage(string path, FitMode initialMode = FitMode.Contain)
    {
        var generation = ++_loadGeneration;
        OpenButton.IsEnabled = false; UpdateStatus("이미지 불러오는 중…");
        try
        {
            var image = await Task.Run(() => ImageLoader.Load(path));
            if (_closing || generation != _loadGeneration) return;
            _imagePath = Path.GetFullPath(path);
            _overlay.Surface.Source = image;
            var view = _overlay.Surface.Viewport;
            view.SetImage(image.PixelWidth, image.PixelHeight);
            // A numeric manual zoom is not saved; restore manual as contain instead.
            view.SetMode(initialMode == FitMode.Manual ? FitMode.Contain : initialMode);
            _overlay.Surface.Refresh(); UpdateStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException or InvalidOperationException or System.Runtime.InteropServices.COMException or OutOfMemoryException)
        {
            if (!_closing && generation == _loadGeneration) UpdateStatus("이미지를 열지 못했습니다: " + ex.Message);
        }
        finally { if (!_closing && generation == _loadGeneration) OpenButton.IsEnabled = true; }
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog { Title = "참고 이미지 열기", Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp|모든 파일|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) LoadImage(dialog.FileName);
    }

    internal void UpdateZoom()
    {
        if (!_ready) return;
        _updatingControls = true;
        ZoomBox.Text = (_overlay.Surface.Viewport.Zoom * 100).ToString("0.##", CultureInfo.CurrentCulture);
        FitBox.SelectedIndex = (int)_overlay.Surface.Viewport.Mode;
        _updatingControls = false;
    }
    private void ZoomBy(double ratio)
    {
        if (!Editing) return;
        var view = _overlay.Surface.Viewport;
        view.ZoomAt(view.Zoom * ratio, new Point(view.Width / 2, view.Height / 2));
        _overlay.Surface.InvalidateVisual(); UpdateZoom();
    }
    private void ApplyZoomText()
    {
        if (!Editing) { UpdateZoom(); return; }
        if (double.TryParse(ZoomBox.Text.Trim().TrimEnd('%'), CultureInfo.CurrentCulture, out var percent) && double.IsFinite(percent) && percent > 0)
        {
            var view = _overlay.Surface.Viewport;
            view.ZoomAt(percent / 100, new Point(view.Width / 2, view.Height / 2));
            _overlay.Surface.InvalidateVisual();
        }
        else UpdateStatus("배율은 양수로 입력해주세요 (5~1600%)");
        UpdateZoom();
    }

    private void ToolbarResizeDown(object sender, MouseButtonEventArgs e)
    {
        if (!Editing) return;
        var point = e.GetPosition(this); var edge = Edges.None;
        if (point.Y < 5) edge |= Edges.Top;
        if (point.X < 5) edge |= Edges.Left;
        if (point.X > ActualWidth - 5) edge |= Edges.Right;
        if (edge == Edges.None) return;
        BeginResize(edge); CaptureMouse(); e.Handled = true;
    }
    internal void BeginResize(Edges edges)
    {
        if (!Editing) return;
        Native.GetCursorPos(out _resizeStart);
        Native.GetWindowRect(Native.Handle(this), out _resizeRect);
        _resizeDpi = Native.GetDpiForWindow(Native.Handle(this)) / 96.0;
        _resizeRect.Bottom += (int)(_bodyHeight * _resizeDpi);
        _resizeEdges = edges;
    }
    internal void UpdateResize()
    {
        if (!Resizing || !Editing) return;
        Native.GetCursorPos(out var cursor);
        var dx = cursor.X - _resizeStart.X; var dy = cursor.Y - _resizeStart.Y;
        var rect = _resizeRect;
        var minW = (int)(MinWidth * _resizeDpi); var minH = (int)((Height + 120) * _resizeDpi);
        if ((_resizeEdges & Edges.Left) != 0) rect.Left = Math.Min(rect.Right - minW, rect.Left + dx);
        if ((_resizeEdges & Edges.Right) != 0) rect.Right = Math.Max(rect.Left + minW, rect.Right + dx);
        if ((_resizeEdges & Edges.Top) != 0) rect.Top = Math.Min(rect.Bottom - minH, rect.Top + dy);
        if ((_resizeEdges & Edges.Bottom) != 0) rect.Bottom = Math.Max(rect.Top + minH, rect.Bottom + dy);
        _bodyHeight = rect.Height / _resizeDpi - Height;
        Native.SetWindowPos(Native.Handle(this), 0, rect.Left, rect.Top, rect.Width, (int)Math.Round(Height * _resizeDpi), Native.NoActivate | Native.NoZOrder);
        SyncBody();
    }
    internal void EndResize() => _resizeEdges = Edges.None;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _closing = true; ++_loadGeneration;
        _overlay.StopGesture();
        foreach (var id in _hotkeys) Native.UnregisterHotKey(Native.Handle(this), id);
        try
        {
            (_settings with
            {
                ScreenX = _lastRect.Left, ScreenY = _lastRect.Top,
                Width = double.IsFinite(Width) ? Width : _settings.Width,
                BodyHeight = _bodyHeight, Opacity = OpacitySlider.Value / 100,
                LastImage = _imagePath, Mode = _overlay.Surface.Viewport.Mode
            }).Save(_settingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Closing must still release owned windows. */ }
        _overlay.Close();
    }

    private void DragBar(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        // Do not convert button, slider, textbox, or combo interactions into window drags.
        var current = e.OriginalSource as DependencyObject;
        while (current is not null && current != sender)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase or Slider or TextBox or ComboBox) return;
            current = current is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(current) : null;
        }
        if (e.ChangedButton == MouseButton.Left && !Resizing) { DragMove(); e.Handled = true; }
    }
    private void EditClick(object sender, RoutedEventArgs e) => SetEditing(EditToggle.IsChecked == true);
    private void OpenClick(object sender, RoutedEventArgs e) => OpenFile();
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void ZoomOutClick(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.15);
    private void ZoomInClick(object sender, RoutedEventArgs e) => ZoomBy(1.15);
    private void CenterClick(object sender, RoutedEventArgs e) { if (!Editing) return; _overlay.Surface.Viewport.Center(); _overlay.Surface.InvalidateVisual(); }
    private void ZoomKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { ApplyZoomText(); e.Handled = true; } }
    private void ZoomLostFocus(object sender, KeyboardFocusChangedEventArgs e) { if (!_updatingControls) ApplyZoomText(); }
    private void FitChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updatingControls || !Editing || FitBox.SelectedIndex < 0) return;
        _overlay.Surface.Viewport.SetMode((FitMode)FitBox.SelectedIndex);
        _overlay.Surface.InvalidateVisual(); UpdateZoom();
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel is not null) OpacityLabel.Text = $"{e.NewValue:0}%";
        if (_overlay is not null) { _overlay.Surface.ContentOpacity = e.NewValue / 100; _overlay.Surface.InvalidateVisual(); }
    }
    private void HelpClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        $"편집 OFF: 본문 클릭·드래그·휠이 뒤 작업창으로 통과합니다.\n편집 ON: 휠 확대, 드래그 이동, 테두리 크기 조절.\n상단 빈 영역 드래그: 두 모드 모두 창 이동.\n\n숨기기/표시: {_hideKey}\n편집 ON/OFF: {_editKey}\nEsc: 편집 OFF (뷰어에 포커스가 있을 때)\nCtrl+O: 이미지 열기 (상단 바에 포커스가 있을 때)\n\n100%는 원본 1픽셀 = 화면 1픽셀입니다.\n설정은 사용자 LocalAppData/ReferenceOverlayViewer에 저장됩니다.",
        "레퍼런스 이미지 뷰어 도움말", MessageBoxButton.OK, MessageBoxImage.Information);
}
