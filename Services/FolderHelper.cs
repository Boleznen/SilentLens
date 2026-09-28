using System;
using System.Diagnostics;
using System.IO;

namespace SilentLens.Services;

public static class FolderHelper
{
    public static void OpenInExplorer(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void RevealInExplorer(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            if (!File.Exists(filePath)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static string GetLogsFolder()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SilentLens", "logs");

    public static string GetSettingsFolder()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SilentLens");

    public static string GetDataFolder()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SilentLens");

    public static string GetSettingsFile()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SilentLens", "settings.json");

    public static string GetOriginalFolder(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? ".";
        return Path.Combine(dir, "_Original");
    }
}