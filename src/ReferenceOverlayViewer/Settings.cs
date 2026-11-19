using System.IO;
using System.Text.Json;

namespace ReferenceOverlayViewer;

public sealed record Settings
{
    public int Version { get; init; } = 1;
    public int? ScreenX { get; init; }
    public int? ScreenY { get; init; }
    public double Width { get; init; } = 760;
    public double BodyHeight { get; init; } = 460;
    public double Opacity { get; init; } = .75;
    public FitMode Mode { get; init; } = FitMode.Contain;
    public string? LastImage { get; init; }
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReferenceOverlayViewer", "settings.json");
    public static Settings Load(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            if (value is null || value.Version != 1) return new();
            return value with
            {
                Width = Valid(value.Width, 520, 4000, 760),
                BodyHeight = Valid(value.BodyHeight, 120, 4000, 460),
                Opacity = Valid(value.Opacity, 0, 1, .75),
                Mode = Enum.IsDefined(value.Mode) ? value.Mode : FitMode.Contain
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    private static double Valid(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
