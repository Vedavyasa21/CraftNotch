using System;

namespace ReNotchPixel.Models;

public class ClipboardItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsPinned { get; set; }
    public bool IsUrl => Uri.TryCreate(Content, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public string PreviewText => Content.Length > 80 ? Content[..80].Replace("\r", " ").Replace("\n", " ") + "..." : Content.Replace("\r", " ").Replace("\n", " ");
    public string TimeAgo => Timestamp.ToString("HH:mm");
}
