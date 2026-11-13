using System;
using System.Windows.Media.Imaging;

namespace ReNotchPixel.Models;

public class MediaInfo
{
    public string Title { get; set; } = "No Media Playing";
    public string Artist { get; set; } = "Start music on your PC";
    public bool IsPlaying { get; set; }
    public TimeSpan Position { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);
    public BitmapSource? Thumbnail { get; set; }
    public string SourceApp { get; set; } = "";

    private double? _manualProgress;
    public double ProgressPercentage
    {
        get => _manualProgress ?? (Duration.TotalSeconds > 0 ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds * 100.0, 0, 100) : 0);
        set => _manualProgress = value;
    }

    public string FormattedPosition => $"{(int)Position.TotalMinutes}:{Position.Seconds:D2}";
    public string FormattedDuration => $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";
}
