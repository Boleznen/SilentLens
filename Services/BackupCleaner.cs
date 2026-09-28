using System;
using System.IO;
using System.Linq;

namespace SilentLens.Services;

/// <summary>
/// Удаление устаревших файлов из папок _Original.
/// </summary>
public static class BackupCleaner
{
    /// <summary>
    /// Удалить из подпапки _Original файлы старше N дней.
    /// Возвращает количество удалённых файлов и освобождённых байт.
    /// </summary>
    public static (int removed, long bytes) CleanOldBackups(string sourceFolder, int maxAgeDays)
    {
        if (maxAgeDays <= 0) return (0, 0);
        if (!Directory.Exists(sourceFolder)) return (0, 0);

        var cutoff = DateTime.Now.AddDays(-maxAgeDays);
        int removed = 0;
        long bytes = 0;

        try
        {
            var originals = Directory.EnumerateDirectories(
                sourceFolder, "_Original", SearchOption.AllDirectories);

            foreach (var origFolder in originals)
            {
                foreach (var file in Directory.EnumerateFiles(origFolder))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.LastWriteTime >= cutoff) continue;

                        bytes += fi.Length;
                        fi.Delete();
                        removed++;
                    }
                    catch { }
                }

                try
                {
                    if (!Directory.EnumerateFileSystemEntries(origFolder).Any())
                        Directory.Delete(origFolder);
                }
                catch { }
            }
        }
        catch { }

        return (removed, bytes);
    }

    /// <summary>
    /// Просканировать указанную папку и удалить старые бэкапы.
    /// </summary>
    public static (int removed, long bytes) CleanFolder(string folder, int maxAgeDays)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return (0, 0);

        return CleanOldBackups(folder, maxAgeDays);
    }
}