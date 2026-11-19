using System.Windows;

namespace ReferenceOverlayViewer;

public enum FitMode { Contain, Width, Height, Actual, Manual }

/// <summary>Zoom is physical screen pixels per source pixel. Coordinates are WPF DIPs.</summary>
public sealed class Viewport
{
    public const double MinZoom = .05, MaxZoom = 16;
    public double ImageWidth { get; private set; }
    public double ImageHeight { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }
    public double Dpi { get; private set; } = 1;
    public double Zoom { get; private set; } = 1;
    public double X { get; private set; }
    public double Y { get; private set; }
    public FitMode Mode { get; private set; } = FitMode.Contain;
    public double DrawWidth => ImageWidth * Zoom / Dpi;
    public double DrawHeight => ImageHeight * Zoom / Dpi;
    public bool HasImage => ImageWidth > 0 && ImageHeight > 0;

    public void SetImage(double width, double height)
    {
        ImageWidth = width; ImageHeight = height;
        Mode = FitMode.Contain; ApplyFit();
    }

    public void Resize(double width, double height, double dpi)
    {
        if (!double.IsFinite(width + height + dpi) || width <= 0 || height <= 0 || dpi <= 0) return;
        var oldW = Width; var oldH = Height; var oldDpi = Dpi;
        // Source point at the old viewport center, before the DPI changes.
        var sourceX = (oldW / 2 - X) * oldDpi / Zoom;
        var sourceY = (oldH / 2 - Y) * oldDpi / Zoom;
        Width = width; Height = height; Dpi = dpi;
        if (Mode is FitMode.Manual)
        {
            X = Width / 2 - sourceX * Zoom / Dpi;
            Y = Height / 2 - sourceY * Zoom / Dpi;
            ClampPan();
        }
        else ApplyFit();
    }

    public void SetMode(FitMode mode)
    {
        Mode = mode;
        if (mode != FitMode.Manual) ApplyFit();
    }

    private void ApplyFit()
    {
        if (!HasImage || Width <= 0 || Height <= 0) return;
        Zoom = Mode switch
        {
            FitMode.Width => Width * Dpi / ImageWidth,
            FitMode.Height => Height * Dpi / ImageHeight,
            FitMode.Actual => 1,
            _ => Math.Min(Width * Dpi / ImageWidth, Height * Dpi / ImageHeight)
        };
        // Fit is exact, including images smaller/larger than the manual zoom limits.
        Center();
    }

    public bool ZoomAt(double zoom, Point anchor)
    {
        if (!HasImage || !double.IsFinite(zoom) || zoom <= 0) return false;
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        var ratio = zoom / Zoom;
        X = anchor.X - (anchor.X - X) * ratio;
        Y = anchor.Y - (anchor.Y - Y) * ratio;
        Zoom = zoom; Mode = FitMode.Manual; ClampPan();
        return true;
    }

    public void Pan(double dx, double dy) { X += dx; Y += dy; ClampPan(); }
    public void Center() { X = (Width - DrawWidth) / 2; Y = (Height - DrawHeight) / 2; }
    private void ClampPan()
    {
        // Keep at least 32 DIPs visible; small images stay centered.
        X = DrawWidth <= Width ? (Width - DrawWidth) / 2 : Math.Clamp(X, 32 - DrawWidth, Width - 32);
        Y = DrawHeight <= Height ? (Height - DrawHeight) / 2 : Math.Clamp(Y, 32 - DrawHeight, Height - 32);
    }
}
