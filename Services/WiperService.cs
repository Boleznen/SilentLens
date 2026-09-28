using System;
using System.IO;
using SilentLens.Core;

namespace SilentLens.Services;

public static class WiperService
{
    public static string GetBackupFolder(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? ".";
        return Path.Combine(dir, "_Original");
    }

    public static string MakeBackup(string filePath)
    {
        var backupDir = GetBackupFolder(filePath);
        Directory.CreateDirectory(backupDir);

        var fileName = Path.GetFileName(filePath);
        var backupPath = Path.Combine(backupDir, fileName);

        if (File.Exists(backupPath))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            backupPath = Path.Combine(backupDir,
                $"{nameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        }

        File.Copy(filePath, backupPath, overwrite: false);
        return backupPath;
    }

    /// <summary>
    /// Удалить метаданные из файла. Формат определяется по СИГНАТУРЕ, а не по расширению.
    /// </summary>
    public static WipeResult WipeFile(string filePath, WipeMode mode, bool saveBackup)
    {
        var result = new WipeResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.Error = "Файл не найден.";
                return result;
            }

            // Определяем реальный формат
            var format = FileFormatDetector.Detect(filePath);

            if (!FileFormatDetector.IsSupported(format))
            {
                result.Success = false;
                result.Error = "Не удалось определить формат файла.";
                return result;
            }

            if (!FileFormatDetector.IsWritable(format))
            {
                result.Success = false;
                result.Error = $"Формат {FileFormatDetector.GetDisplayName(format)} " +
                               "поддерживается только для чтения.";
                return result;
            }

            // Бэкап
            if (saveBackup)
            {
                try
                {
                    result.OriginalBackupPath = MakeBackup(filePath);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Не удалось создать резервную копию: {ex.Message}";
                    return result;
                }
            }

            // Выбор wiper'а по РЕАЛЬНОМУ формату
            switch (format)
            {
                case ImageFormat.Jpeg:
                    return MergeWithBackup(JpegWiper.Wipe(filePath, mode), result);

                case ImageFormat.Png:
                    return MergeWithBackup(PngWiper.Wipe(filePath, mode), result);

                case ImageFormat.Webp:
                    return MergeWithBackup(WebpWiper.Wipe(filePath, mode), result);

                default:
                    result.Success = false;
                    result.Error = $"Формат {FileFormatDetector.GetDisplayName(format)} " +
                                   "не поддерживается для удаления.";
                    return result;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    private static WipeResult MergeWithBackup(WipeResult inner, WipeResult outer)
    {
        outer.Success = inner.Success;
        outer.Error = inner.Error;
        outer.OriginalSize = inner.OriginalSize;
        outer.NewSize = inner.NewSize;
        return outer;
    }
}