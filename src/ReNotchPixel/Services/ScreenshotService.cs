using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Windows;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace ReNotchPixel.Services;

public static class ScreenshotService
{
    public static void TriggerSnip()
    {
        try
        {
            // Launch Windows modern Snipping Tool (Win+Shift+S)
            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        }
        catch
        {
            CaptureFullScreen();
        }
    }

    public static string CaptureFullScreen()
    {
        try
        {
            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            using var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));
            }

            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string screenshotsDir = Path.Combine(pictures, "Screenshots");
            if (!Directory.Exists(screenshotsDir)) Directory.CreateDirectory(screenshotsDir);

            string filename = $"ReNotch_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(screenshotsDir, filename);
            bitmap.Save(fullPath, ImageFormat.Png);

            // Copy to clipboard
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var handle = bitmap.GetHbitmap();
                    var wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        handle, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    Clipboard.SetImage(wpfBitmap);
                }
                catch { }
            });

            SystemSounds.Asterisk.Play();
            return fullPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Error: {ex.Message}");
            return "";
        }
    }
}
