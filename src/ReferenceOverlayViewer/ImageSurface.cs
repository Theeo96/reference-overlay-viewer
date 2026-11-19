using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReferenceOverlayViewer;

public sealed class ImageSurface : FrameworkElement
{
    public Viewport Viewport { get; } = new();
    public BitmapSource? Source { get; set; }
    public double ContentOpacity { get; set; } = .75;
    public bool Editing { get; set; }
    public string Hint { get; set; } = "상단의 ‘열기’로 참고 이미지를 선택하세요";
    public event Action? ViewChanged;

    public ImageSurface()
    {
        ClipToBounds = true;
        SizeChanged += (_, _) => Refresh();
    }
    public void Refresh()
    {
        Viewport.Resize(ActualWidth, ActualHeight, VisualTreeHelper.GetDpi(this).DpiScaleX);
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        // A near-transparent hit surface keeps ON interactive even at visual opacity 0.
        // OFF input passthrough is enforced at HWND level, independently of rendered alpha.
        if (Editing) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), null, bounds);
        dc.PushOpacity(ContentOpacity);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(23, 30, 44)), null, bounds);
        if (Source is not null && Viewport.HasImage)
            dc.DrawImage(Source, new Rect(Viewport.X, Viewport.Y, Viewport.DrawWidth, Viewport.DrawHeight));
        else
        {
            var text = new FormattedText(Hint, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Malgun Gothic"), 14, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            text.MaxTextWidth = Math.Max(10, ActualWidth - 40);
            dc.DrawText(text, new Point(20, Math.Max(20, (ActualHeight - text.Height) / 2)));
        }
        if (Editing && ActualWidth > 2 && ActualHeight > 2)
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), 2), new Rect(1, 1, ActualWidth - 2, ActualHeight - 2));
        dc.Pop();
    }
}
