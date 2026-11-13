using System;
using System.IO;

namespace ReNotchPixel.Models;

public class ShelfItem
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();
    public long FileSizeBytes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;

    public string FormattedSize
    {
        get
        {
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
