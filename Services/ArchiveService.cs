using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using SilentLens.Models;

namespace SilentLens.Services;

public static class ArchiveService
{
    /// <summary>
    /// Собрать все _Original-папки выбранных файлов в один ZIP.
    /// Возвращает количество добавленных файлов и путь к архиву.
    /// </summary>
    public static (int added, string zipPath) ArchiveOriginals(
        IEnumerable<PhotoItem> items, string zipPath)
    {
        int added = 0;

        try
        {
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var origFolder = FolderHelper.GetOriginalFolder(item.FilePath);
                if (!Directory.Exists(origFolder)) continue;

                foreach (var file in Directory.EnumerateFiles(origFolder, "*", SearchOption.AllDirectories))
                {
                    if (!seen.Add(file)) continue;

                    try
                    {
                        var entryName = Path.GetFileName(file);

                        // Если файл с таким именем уже в архиве — добавляем префикс
                        var entry = zip.GetEntry(entryName);
                        if (entry != null)
                        {
                            var folder = Path.GetFileName(Path.GetDirectoryName(file)) ?? "backup";
                            entryName = $"{folder}/{entryName}";
                        }

                        zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                        added++;
                    }
                    catch { }
                }
            }
        }
        catch { }

        return (added, zipPath);
    }

    /// <summary>
    /// Список всех файлов-оригиналов для указанных элементов.
    /// </summary>
    public static List<string> GetOriginalFiles(IEnumerable<PhotoItem> items)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var origFolder = FolderHelper.GetOriginalFolder(item.FilePath);
            if (!Directory.Exists(origFolder)) continue;

            foreach (var file in Directory.EnumerateFiles(origFolder, "*", SearchOption.AllDirectories))
            {
                if (seen.Add(file))
                    result.Add(file);
            }
        }

        return result;
    }
}