using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReferenceOverlayViewer;

/// <summary>Opt-in, windowless regression suite. Does not simulate desktop input.</summary>
internal static class SelfTest
{
    internal static void Run(string reportPath)
    {
        var results = new List<object>();
        var root = Path.Combine(Path.GetTempPath(), "ReferenceOverlayViewer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Check(string name, Action test)
        {
            try { test(); results.Add(new { name, passed = true, error = (string?)null }); }
            catch (Exception ex) { results.Add(new { name, passed = false, error = ex.ToString() }); }
        }
        static void Assert(bool ok, string message = "Assertion failed") { if (!ok) throw new InvalidOperationException(message); }
        static void Near(double actual, double expected) => Assert(Math.Abs(actual - expected) < .00001, $"Expected {expected}, got {actual}");
        static Viewport View(double w = 800, double h = 600, double iw = 1600, double ih = 900, double dpi = 1)
        {
            var v = new Viewport(); v.Resize(w, h, dpi); v.SetImage(iw, ih); return v;
        }
        try
        {
            Check("contain-landscape", () => { var v = View(); Near(v.Zoom, .5); Near(v.Y, 75); });
            Check("contain-portrait", () => { var v = View(iw: 600, ih: 1200); Near(v.Zoom, .5); Near(v.X, 250); });
            Check("contain-square", () => { var v = View(iw: 1000, ih: 1000); Near(v.Zoom, .6); Near(v.X, 100); });
            Check("width-crops-vertical", () => { var v = View(iw: 600, ih: 1200); v.SetMode(FitMode.Width); Near(v.DrawWidth, 800); Assert(v.DrawHeight > 600); });
            Check("height-crops-horizontal", () => { var v = View(); v.SetMode(FitMode.Height); Near(v.DrawHeight, 600); Assert(v.DrawWidth > 800); });
            Check("fit-resize", () => { var v = View(); v.Resize(400, 300, 1); Near(v.Zoom, .25); });
            Check("actual-pixels-at-each-dpi", () => { foreach (var dpi in new[] { 1.0, 1.5, 2.0 }) { var v = View(dpi: dpi); v.SetMode(FitMode.Actual); Near(v.DrawWidth * dpi, 1600); } });
            Check("zoom-anchor-source-stable", () => { var v = View(); var p = new Point(450, 250); var x = (p.X - v.X) / v.Zoom; var y = (p.Y - v.Y) / v.Zoom; v.ZoomAt(1, p); Near((p.X - v.X) / v.Zoom, x); Near((p.Y - v.Y) / v.Zoom, y); });
            Check("manual-resize-center-stable", () => { var v = View(); v.ZoomAt(2, new Point(400, 300)); var x = (400 - v.X) / v.Zoom; v.Resize(900, 700, 1); Near(v.Zoom, 2); Near((450 - v.X) / v.Zoom, x); });
            Check("manual-dpi-center-stable", () => { var v = View(); v.ZoomAt(2, new Point(400, 300)); var x = (400 - v.X) / v.Zoom; v.Resize(800, 600, 1.5); Near((400 - v.X) * 1.5 / v.Zoom, x); });
            Check("manual-invalid-and-clamps", () => { var v = View(); Assert(!v.ZoomAt(double.NaN, new Point())); Assert(!v.ZoomAt(-1, new Point())); v.ZoomAt(100, new Point()); Near(v.Zoom, 16); v.ZoomAt(.001, new Point()); Near(v.Zoom, .05); });
            Check("pan-does-not-lose-image", () => { var v = View(); v.SetMode(FitMode.Actual); v.Pan(1e8, 1e8); Assert(v.X < v.Width && v.Y < v.Height); v.Pan(-2e8, -2e8); Assert(v.X + v.DrawWidth > 0 && v.Y + v.DrawHeight > 0); });
            Check("exact-fit-beyond-manual-limits", () => { var v = View(iw: 40000, ih: 40000); Near(v.DrawHeight, 600); Assert(v.Zoom < Viewport.MinZoom); });
            Check("settings-corrupt-defaults", () => { var path = Path.Combine(root, "bad.json"); File.WriteAllText(path, "oops"); Assert(Settings.Load(path).Width == 760); });
            Check("settings-normalize", () => { var path = Path.Combine(root, "normalize.json"); File.WriteAllText(path, "{\"Width\":-1,\"BodyHeight\":99999,\"Opacity\":5,\"Mode\":99}"); var s = Settings.Load(path); Near(s.Width, 520); Near(s.BodyHeight, 4000); Near(s.Opacity, 1); Assert(s.Mode == FitMode.Contain); });
            Check("settings-round-trip", () => { var path = Path.Combine(root, "settings.json"); var s = new Settings { ScreenX = -1200, Width = 860, Opacity = .42, Mode = FitMode.Height }; s.Save(path); Assert(s == Settings.Load(path)); Assert(!File.Exists(path + ".tmp")); });
            Check("settings-unknown-version", () => { var path = Path.Combine(root, "future.json"); File.WriteAllText(path, "{\"Version\":99,\"Width\":1000}"); Near(Settings.Load(path).Width, 760); });

            var pixels = new byte[] { 0, 0, 255, 255, 0, 255, 0, 128, 255, 0, 0, 255, 0, 255, 255, 255, 255, 0, 255, 255, 255, 255, 0, 255 };
            var source = BitmapSource.Create(3, 2, 144, 144, PixelFormats.Bgra32, null, pixels, 12);
            foreach (var (ext, encoder) in new (string, BitmapEncoder)[] { ("png", new PngBitmapEncoder()), ("bmp", new BmpBitmapEncoder()), ("jpg", new JpegBitmapEncoder()) })
            {
                Check("decode-and-release-" + ext, () =>
                {
                    var file = Path.Combine(root, "sample." + ext);
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    using (var output = File.Create(file)) encoder.Save(output);
                    var bitmap = ImageLoader.Load(file);
                    Assert(bitmap.IsFrozen); Assert(bitmap.PixelWidth == 3 && bitmap.PixelHeight == 2);
                    if (ext == "png") { var data = new byte[24]; bitmap.CopyPixels(data, 12, 0); Assert(data[7] == 128); }
                    var renamed = file + ".renamed"; File.Move(file, renamed); File.Delete(renamed);
                    var retained = new byte[24]; bitmap.CopyPixels(retained, 12, 0);
                });
            }
            Check("exif-rotation-from-jpeg", () =>
            {
                var metadata = new BitmapMetadata("jpg"); metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
                var encoder = new JpegBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
                var file = Path.Combine(root, "rotate.jpg"); using (var output = File.Create(file)) encoder.Save(output);
                var bitmap = ImageLoader.Load(file); Assert(bitmap.PixelWidth == 2 && bitmap.PixelHeight == 3);
            });
            Check("all-eight-exif-pixel-maps", () =>
            {
                var expected = new[] { new[] { 0, 1, 2, 3, 4, 5 }, new[] { 2, 1, 0, 5, 4, 3 }, new[] { 5, 4, 3, 2, 1, 0 }, new[] { 3, 4, 5, 0, 1, 2 }, new[] { 0, 3, 1, 4, 2, 5 }, new[] { 3, 0, 4, 1, 5, 2 }, new[] { 5, 2, 4, 1, 3, 0 }, new[] { 2, 5, 1, 4, 0, 3 } };
                for (ushort orientation = 1; orientation <= 8; orientation++)
                {
                    var oriented = ImageLoader.Orient(source, orientation); var output = new byte[24];
                    oriented.CopyPixels(output, oriented.PixelWidth * 4, 0);
                    for (var pixel = 0; pixel < 6; pixel++) for (var channel = 0; channel < 4; channel++)
                        Assert(output[pixel * 4 + channel] == pixels[expected[orientation - 1][pixel] * 4 + channel], $"EXIF {orientation}, pixel {pixel}");
                }
            });
            Check("reject-corrupt-image", () => { var file = Path.Combine(root, "corrupt.png"); File.WriteAllText(file, "not an image"); var rejected = false; try { ImageLoader.Load(file); } catch (Exception) { rejected = true; } Assert(rejected); });
            Check("reject-unsupported-format", () => { var file = Path.Combine(root, "sample.gif"); File.WriteAllText(file, "test"); var rejected = false; try { ImageLoader.Load(file); } catch (InvalidDataException) { rejected = true; } Assert(rejected); });
            Check("reject-oversized-file", () => { var file = Path.Combine(root, "oversize.bmp"); using (var output = File.Create(file)) output.SetLength(256L * 1024 * 1024 + 1); var rejected = false; try { ImageLoader.Load(file); } catch (InvalidDataException) { rejected = true; } Assert(rejected); });
            Check("surface-render-alpha", () =>
            {
                var surface = new ImageSurface { ContentOpacity = .5 }; surface.Measure(new Size(100, 100)); surface.Arrange(new Rect(0, 0, 100, 100));
                var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
                var data = new byte[40000]; bitmap.CopyPixels(data, 400, 0); Assert(data[3] is >= 127 and <= 130);
            });
            Check("surface-zero-opacity-off-is-transparent", () =>
            {
                var surface = new ImageSurface { ContentOpacity = 0 }; surface.Measure(new Size(100, 100)); surface.Arrange(new Rect(0, 0, 100, 100));
                var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
                var data = new byte[40000]; bitmap.CopyPixels(data, 400, 0); Assert(data.All(value => value == 0));
            });
            Check("toolbar-mode-contract-and-offscreen-layout", () =>
            {
                var foreground = Native.GetForegroundWindow();
                var toolbar = new ToolbarWindow(new Settings(), Path.Combine(root, "isolated-settings.json"), false);
                _ = new WindowInteropHelper(toolbar).EnsureHandle();
                var body = toolbar.Overlay;
                _ = new WindowInteropHelper(body).EnsureHandle();
                try
                {
                    var view = body.Surface.Viewport; view.Resize(760, 460, 1); view.SetImage(1600, 900);
                    view.ZoomAt(1.3, new Point(380, 230)); view.Pan(50, 20);
                    var snapshot = (view.Zoom, view.X, view.Y);
                    foreach (var edit in new[] { true, false, true, false })
                    {
                        toolbar.SetEditing(edit); Assert(toolbar.Editing == edit && body.Editing == edit);
                        Assert(toolbar.EditControls.IsEnabled == edit && toolbar.EditToggle.IsChecked == edit);
                        Assert((view.Zoom, view.X, view.Y) == snapshot);
                        Assert((Native.Style(Native.Handle(body)) & Native.ExTransparent) != 0 == !edit);
                        Near(toolbar.Opacity, 1);
                    }
                    foreach (var width in new[] { 520.0, 760.0 })
                    {
                        var content = (FrameworkElement)toolbar.Content;
                        content.Measure(new Size(width, 112)); content.Arrange(new Rect(0, 0, width, 112)); content.UpdateLayout();
                        var controls = toolbar.EditControls.TransformToAncestor(content).TransformBounds(new Rect(toolbar.EditControls.RenderSize));
                        Assert(controls.Left >= 0 && controls.Right <= width, "Edit controls exceed toolbar width");
                        var open = toolbar.OpenButton.TransformToAncestor(content).TransformBounds(new Rect(toolbar.OpenButton.RenderSize));
                        Assert(open.Left >= 0 && open.Right <= width, "Open button exceeds toolbar width");
                        var bitmap = new RenderTargetBitmap((int)width, 112, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, $"toolbar-{width:0}.png")); encoder.Save(output);
                    }
                    Assert(foreground == Native.GetForegroundWindow());
                }
                finally { toolbar.Close(); }
            });
            Check("native-policy-toggle-hidden-hwnd", () =>
            {
                var foreground = Native.GetForegroundWindow();
                var window = new Window { AllowsTransparency = true, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };
                var handle = new WindowInteropHelper(window).EnsureHandle();
                try
                {
                    Native.SetInputPolicy(handle, false);
                    Assert((Native.Style(handle) & (Native.ExLayered | Native.ExTransparent | Native.ExNoActivate)) == (Native.ExLayered | Native.ExTransparent | Native.ExNoActivate));
                    Native.SetInputPolicy(handle, true);
                    Assert((Native.Style(handle) & (Native.ExTransparent | Native.ExNoActivate)) == 0);
                    Assert((Native.Style(handle) & Native.ExLayered) != 0);
                    Assert(foreground == Native.GetForegroundWindow(), "Hidden HWND policy check changed foreground");
                }
                finally { window.Close(); }
            });
            Check("closing-image-window-closes-viewer-group", () =>
            {
                var toolbar = new ToolbarWindow(new Settings(), Path.Combine(root, "close-settings.json"), false);
                _ = new WindowInteropHelper(toolbar).EnsureHandle();
                _ = new WindowInteropHelper(toolbar.Overlay).EnsureHandle();
                toolbar.Overlay.Close();
                toolbar.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                Assert(toolbar.IsClosing && Native.Handle(toolbar) == 0 && Native.Handle(toolbar.Overlay) == 0);
            });
        }
        finally
        {
            // Delete only this uniquely created test directory.
            Directory.Delete(root, true);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), os = Environment.OSVersion.ToString(), results }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (results.Any(result => JsonSerializer.Serialize(result).Contains("\"passed\":false", StringComparison.Ordinal)))
            throw new InvalidOperationException("Regression suite failed. See JSON report.");
    }
}
