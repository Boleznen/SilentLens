using System;
using System.IO;

namespace SilentLens.Core;

/// <summary>
/// Реальный формат изображения, определённый по сигнатуре (magic bytes).
/// </summary>
public enum ImageFormat
{
    Unknown,
    Jpeg,
    Png,
    Webp,
    Tiff,
    Bmp,
    Gif,
    Heic,
    Heif,
    Avif,
    Cr2,
    Nef,
    Arw,
    Dng,
    Raf,
    Rw2,
    Orf
}

public static class FileFormatDetector
{
    /// <summary>
    /// Определить реальный формат файла по первым байтам.
    /// </summary>
    public static ImageFormat Detect(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath)) return ImageFormat.Unknown;
            if (!File.Exists(filePath)) return ImageFormat.Unknown;

            // Читаем первые 16 байт — этого достаточно для всех сигнатур
            var buffer = new byte[16];
            int read;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, buffer.Length))
            {
                read = fs.Read(buffer, 0, buffer.Length);
            }

            if (read < 2) return ImageFormat.Unknown;

            // JPEG: FF D8 FF
            if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            {
                // Отличаем CR2 (Canon RAW) от обычного JPEG
                if (read >= 12)
                {
                    // CR2: ..."CR" в позиции 8-9
                    if (buffer[8] == 'C' && buffer[9] == 'R' && buffer[10] == 0x02 && buffer[11] == 0x00)
                        return ImageFormat.Cr2;
                }
                return ImageFormat.Jpeg;
            }

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47
                && buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A)
                return ImageFormat.Png;

            // GIF: "GIF87a" или "GIF89a"
            if (buffer[0] == 'G' && buffer[1] == 'I' && buffer[2] == 'F' && buffer[3] == '8')
                return ImageFormat.Gif;

            // BMP: "BM"
            if (buffer[0] == 'B' && buffer[1] == 'M')
                return ImageFormat.Bmp;

            // TIFF / DNG / NEF / ARW / RW2 / ORF / RAF (все — TIFF-подобные):
            // - II*\0 (Little-endian) 49 49 2A 00
            // - MM\0* (Big-endian)    4D 4D 00 2A
            bool isTiffLE = buffer[0] == 0x49 && buffer[1] == 0x49 && buffer[2] == 0x2A && buffer[3] == 0x00;
            bool isTiffBE = buffer[0] == 0x4D && buffer[1] == 0x4D && buffer[2] == 0x00 && buffer[3] == 0x2A;

            if (isTiffLE || isTiffBE)
            {
                // Отличить RAW-форматы от чистого TIFF сложно без глубокого парсинга.
                // По расширению файла уточняем:
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                return ext switch
                {
                    ".dng" => ImageFormat.Dng,
                    ".nef" => ImageFormat.Nef,
                    ".arw" => ImageFormat.Arw,
                    ".rw2" => ImageFormat.Rw2,
                    ".orf" => ImageFormat.Orf,
                    _ => ImageFormat.Tiff
                };
            }

            // WebP: RIFF....WEBP
            if (buffer[0] == 'R' && buffer[1] == 'I' && buffer[2] == 'F' && buffer[3] == 'F'
                && buffer[8] == 'W' && buffer[9] == 'E' && buffer[10] == 'B' && buffer[11] == 'P')
                return ImageFormat.Webp;

            // HEIC / HEIF / AVIF: ....ftypXXXX
            if (buffer[4] == 'f' && buffer[5] == 't' && buffer[6] == 'y' && buffer[7] == 'p')
            {
                // "ftypheic", "ftypheix", "ftypmif1", "ftypavif", ...
                var brand = System.Text.Encoding.ASCII.GetString(buffer, 8, 4).ToLowerInvariant();
                return brand switch
                {
                    "avif" => ImageFormat.Avif,
                    "heic" => ImageFormat.Heic,
                    "heix" => ImageFormat.Heic,
                    "hevc" => ImageFormat.Heic,
                    "hevx" => ImageFormat.Heic,
                    "mif1" => ImageFormat.Heif,
                    "msf1" => ImageFormat.Heif,
                    _ => ImageFormat.Heic
                };
            }

            // RAF (Fujifilm RAW): "FUJIFILMCCD-RAW"
            if (buffer[0] == 'F' && buffer[1] == 'U' && buffer[2] == 'J' && buffer[3] == 'I')
                return ImageFormat.Raf;

            return ImageFormat.Unknown;
        }
        catch
        {
            return ImageFormat.Unknown;
        }
    }

    /// <summary>
    /// Проверить, является ли формат поддерживаемым для чтения (в том числе только для чтения).
    /// </summary>
    public static bool IsSupported(ImageFormat format)
    {
        return format is not ImageFormat.Unknown;
    }

    /// <summary>
    /// Проверить, можно ли УДАЛЯТЬ метаданные из этого формата (не только читать).
    /// </summary>
    public static bool IsWritable(ImageFormat format)
    {
        return format is ImageFormat.Jpeg or ImageFormat.Png or ImageFormat.Webp;
    }

    /// <summary>
    /// Человекочитаемое имя формата.
    /// </summary>
    public static string GetDisplayName(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.Jpeg => "JPEG",
            ImageFormat.Png => "PNG",
            ImageFormat.Webp => "WebP",
            ImageFormat.Tiff => "TIFF",
            ImageFormat.Bmp => "BMP",
            ImageFormat.Gif => "GIF",
            ImageFormat.Heic => "HEIC",
            ImageFormat.Heif => "HEIF",
            ImageFormat.Avif => "AVIF",
            ImageFormat.Cr2 => "Canon RAW (CR2)",
            ImageFormat.Nef => "Nikon RAW (NEF)",
            ImageFormat.Arw => "Sony RAW (ARW)",
            ImageFormat.Dng => "Adobe DNG",
            ImageFormat.Raf => "Fujifilm RAW (RAF)",
            ImageFormat.Rw2 => "Panasonic RAW (RW2)",
            ImageFormat.Orf => "Olympus RAW (ORF)",
            _ => "Неизвестный"
        };
    }
}