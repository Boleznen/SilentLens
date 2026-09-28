using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using SilentLens.Models;

namespace SilentLens.Services;

public static class MetadataExporter
{
    /// <summary>
    /// Экспорт метаданных одного файла в TXT.
    /// </summary>
    public static string ExportTxt(PhotoItem item)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Файл: {item.FileName}");
        sb.AppendLine($"Путь: {item.FilePath}");
        sb.AppendLine($"Размер: {item.FileSizeFormatted}");
        sb.AppendLine($"Дата изменения: {item.FileModified:yyyy-MM-dd HH:mm:ss}");

        if (item.DateTaken.HasValue)
            sb.AppendLine($"Дата съёмки: {item.DateTaken.Value:yyyy-MM-dd HH:mm:ss}");

        sb.AppendLine(new string('─', 80));

        if (item.Metadata != null)
        {
            foreach (var group in item.Metadata)
            {
                if (group.IsEmpty) continue;

                sb.AppendLine();
                sb.AppendLine($"=== {group.Title} ===");
                foreach (var entry in group.Entries)
                    sb.AppendLine($"{entry.Key}: {entry.Value}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Экспорт метаданных нескольких файлов в TXT.
    /// </summary>
    public static string ExportTxt(IEnumerable<PhotoItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Silent Lens — экспорт метаданных");
        sb.AppendLine($"Дата экспорта: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('═', 80));
        sb.AppendLine();

        foreach (var item in items)
        {
            sb.AppendLine(ExportTxt(item));
            sb.AppendLine();
            sb.AppendLine(new string('═', 80));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Экспорт метаданных одного файла в JSON.
    /// </summary>
    public static string ExportJson(PhotoItem item)
    {
        var obj = new Dictionary<string, object?>
        {
            ["fileName"] = item.FileName,
            ["filePath"] = item.FilePath,
            ["fileSize"] = item.FileSize,
            ["fileModified"] = item.FileModified.ToString("o"),
            ["dateTaken"] = item.DateTaken?.ToString("o"),
            ["isReadOnly"] = item.IsReadOnly,
            ["latitude"] = item.Latitude,
            ["longitude"] = item.Longitude,
            ["groups"] = BuildGroups(item.Metadata)
        };

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Экспорт метаданных нескольких файлов в JSON.
    /// </summary>
    public static string ExportJson(IEnumerable<PhotoItem> items)
    {
        var list = new List<object>();

        foreach (var item in items)
        {
            list.Add(new Dictionary<string, object?>
            {
                ["fileName"] = item.FileName,
                ["filePath"] = item.FilePath,
                ["fileSize"] = item.FileSize,
                ["fileModified"] = item.FileModified.ToString("o"),
                ["dateTaken"] = item.DateTaken?.ToString("o"),
                ["isReadOnly"] = item.IsReadOnly,
                ["latitude"] = item.Latitude,
                ["longitude"] = item.Longitude,
                ["groups"] = BuildGroups(item.Metadata)
            });
        }

        var wrapper = new Dictionary<string, object?>
        {
            ["exportedAt"] = DateTime.Now.ToString("o"),
            ["count"] = list.Count,
            ["files"] = list
        };

        return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<Dictionary<string, object?>> BuildGroups(List<MetadataGroup>? groups)
    {
        var result = new List<Dictionary<string, object?>>();
        if (groups == null) return result;

        foreach (var g in groups)
        {
            if (g.IsEmpty) continue;

            var entries = new List<Dictionary<string, string>>();
            foreach (var e in g.Entries)
            {
                entries.Add(new Dictionary<string, string>
                {
                    ["key"] = e.Key,
                    ["value"] = e.Value
                });
            }

            result.Add(new Dictionary<string, object?>
            {
                ["key"] = g.Key,
                ["title"] = g.Title,
                ["entries"] = entries,
                ["hasGps"] = g.HasGps,
                ["latitude"] = g.Latitude,
                ["longitude"] = g.Longitude
            });
        }

        return result;
    }
}