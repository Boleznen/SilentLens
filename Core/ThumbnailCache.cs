using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace SilentLens.Core;

/// <summary>
/// Кэш миниатюр + загрузка превью с учётом EXIF-ориентации + чтение даты съёмки.
/// </summary>
public static class ThumbnailCache
{
    private const int ThumbnailWidth = 36;

    private static readonly ConcurrentDictionary<string, ImageSource> _cache = new();

    /// <summary>
    /// Загрузить миниатюру 36×36 (с учётом EXIF-ориентации).
    /// </summary>
    public static ImageSource? GetOrLoad(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        if (_cache.TryGetValue(filePath, out var cached)) return cached;

        try
        {
            var rotation = GetExifRotation(filePath);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = ThumbnailWidth * 4;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.Rotation = rotation;
            bitmap.EndInit();
            bitmap.Freeze();

            _cache[filePath] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Получить вращение по EXIF-тегу Orientation.
    /// </summary>
    public static Rotation GetExifRotation(string filePath)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(filePath);
            var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 == null) return Rotation.Rotate0;

            if (!ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation))
                return Rotation.Rotate0;

            return orientation switch
            {
                3 => Rotation.Rotate180,
                6 => Rotation.Rotate90,
                8 => Rotation.Rotate270,
                _ => Rotation.Rotate0
            };
        }
        catch
        {
            return Rotation.Rotate0;
        }
    }

    /// <summary>
    /// Загрузить полноразмерное превью (с ограничением размера) с учётом EXIF-ориентации.
    /// </summary>
    public static ImageSource? LoadFullImage(string filePath, int maxSize = 1200)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        try
        {
            var rotation = GetExifRotation(filePath);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.Rotation = rotation;

            if (maxSize > 0)
                bitmap.DecodePixelWidth = maxSize;

            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Прочитать EXIF-дату съёмки (DateTimeOriginal → DateTimeDigitized → DateTime).
    /// </summary>
    public static DateTime? ReadDateTaken(string filePath)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(filePath);

            var subIfd = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd != null &&
                subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                return dt;

            if (subIfd != null &&
                subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var dt2))
                return dt2;

            var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null &&
                ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dt3))
                return dt3;

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Полная очистка кэша.
    /// </summary>
    public static void Clear() => _cache.Clear();
}