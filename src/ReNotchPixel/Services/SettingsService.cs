using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using ReNotchPixel.Models;

namespace ReNotchPixel.Services;

public class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ReNotchPixel");
    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "ReNotchPixel";

    public AppSettings Settings { get; private set; } = new AppSettings();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        // Sync startup setting with actual registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            Settings.StartWithWindows = key?.GetValue(AppRegistryName) != null;
        }
        catch { }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);

            // Update Registry Run Key
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key != null)
            {
                if (Settings.StartWithWindows)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppRegistryName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    if (key.GetValue(AppRegistryName) != null)
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
