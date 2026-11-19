using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReferenceOverlayViewer;

public static class ImageLoader
{
    // The decoded BGRA image can be at most ~384 MB, excluding temporary decoder buffers.
    public const long MaxPixels = 96_000_000;
    public static BitmapSource Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 256L * 1024 * 1024) throw new InvalidDataException("파일 크기 제한은 256 MB입니다.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp")) throw new InvalidDataException("PNG, JPG, BMP 파일을 선택해주세요.");
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > MaxPixels) throw new InvalidDataException("이미지 해상도 제한은 9,600만 픽셀입니다.");
        ushort orientation = 1;
        if (frame.Metadata is BitmapMetadata metadata)
        {
            try { if (metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort value) orientation = value; }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException) { }
        }
        // Normalize before copying to release every dependency on the input stream.
        BitmapSource source = frame.Format == PixelFormats.Bgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        source = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        source = Orient(source, orientation);
        source.Freeze();
        return source;
    }

    internal static BitmapSource Orient(BitmapSource source, ushort orientation)
    {
        // EXIF transforms include mirrored variants, not just rotations.
        Transform? transform = orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new MatrixTransform(0, 1, 1, 0, 0, 0),
            6 => new RotateTransform(90),
            7 => new MatrixTransform(0, -1, -1, 0, 0, 0),
            8 => new RotateTransform(270),
            _ => null
        };
        return transform is null ? source : new TransformedBitmap(source, transform);
    }
}
