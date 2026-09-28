using System;
using System.Collections.Generic;
using System.IO;

namespace SilentLens.Core;

/// <summary>
/// Своя реализация удаления метаданных из JPEG.
/// Работает на уровне сегментов, без пережатия пиксельных данных.
/// </summary>
public static class JpegWiper
{
    // JPEG маркеры
    private const byte MarkerPrefix = 0xFF;
    private const byte SOI = 0xD8;   // Start of Image
    private const byte EOI = 0xD9;   // End of Image
    private const byte SOS = 0xDA;   // Start of Scan
    private const byte APP0 = 0xE0;  // JFIF
    private const byte APP1 = 0xE1;  // EXIF/XMP
    private const byte APP2 = 0xE2;  // FlashPix/ICC
    private const byte APP13 = 0xED; // IPTC
    private const byte APP14 = 0xEE; // Adobe
    private const byte COM = 0xFE;   // Комментарий

    public static WipeResult Wipe(string filePath, WipeMode mode)
    {
        var result = new WipeResult();
        try
        {
            var original = File.ReadAllBytes(filePath);
            result.OriginalSize = original.Length;

            var output = ProcessJpeg(original, mode, out var removedCount);

            if (removedCount == 0)
            {
                result.Success = true;
                result.NewSize = original.Length;
                return result;
            }

            File.WriteAllBytes(filePath, output);
            result.NewSize = output.Length;
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Пройти по сегментам JPEG и вернуть новый массив без выбранных сегментов.
    /// </summary>
    private static byte[] ProcessJpeg(byte[] input, WipeMode mode, out int removedCount)
    {
        removedCount = 0;

        if (input.Length < 4 || input[0] != MarkerPrefix || input[1] != SOI)
            throw new InvalidDataException("Не JPEG-файл (нет SOI).");

        var output = new List<byte>(input.Length);

        // Пишем SOI
        output.Add(input[0]);
        output.Add(input[1]);

        int pos = 2;

        while (pos < input.Length - 1)
        {
            // Ищем следующий маркер
            if (input[pos] != MarkerPrefix)
            {
                // Защита от битых данных — копируем как есть
                output.Add(input[pos]);
                pos++;
                continue;
            }

            byte marker = input[pos + 1];

            // SOS — начало сжатых данных. Копируем всё до конца файла и выходим.
            if (marker == SOS)
            {
                for (int i = pos; i < input.Length; i++)
                    output.Add(input[i]);
                break;
            }

            // Маркеры без длины (SOI, EOI, RST0-7, TEM)
            if (marker == SOI || marker == EOI
                || (marker >= 0xD0 && marker <= 0xD7)
                || marker == 0x01)
            {
                output.Add(input[pos]);
                output.Add(input[pos + 1]);
                pos += 2;
                continue;
            }

            // Все остальные — сегменты с длиной
            if (pos + 3 >= input.Length)
            {
                for (int i = pos; i < input.Length; i++) output.Add(input[i]);
                break;
            }

            int length = (input[pos + 2] << 8) | input[pos + 3];
            int segmentEnd = pos + 2 + length;

            if (segmentEnd > input.Length || length < 2)
            {
                for (int i = pos; i < input.Length; i++) output.Add(input[i]);
                break;
            }

            // Решаем, удалять ли сегмент
            bool shouldRemove = ShouldRemove(marker, input, pos, mode);

            if (shouldRemove)
            {
                removedCount++;
            }
            else
            {
                for (int i = pos; i < segmentEnd; i++)
                    output.Add(input[i]);
            }

            pos = segmentEnd;
        }

        return output.ToArray();
    }

    private static bool ShouldRemove(byte marker, byte[] input, int pos, WipeMode mode)
    {
        switch (mode)
        {
            case WipeMode.All:
                // Удаляем всё, кроме APP0 (JFIF)
                return marker is APP1 or APP2 or APP13 or APP14 or COM;

            case WipeMode.GpsOnly:
                // Удаляем только APP1, если в нём есть GPS
                if (marker != APP1) return false;
                return ContainsGpsTag(input, pos);

            case WipeMode.KeepDate:
                // Удаляем всё, кроме APP1 (EXIF). В APP1 остаётся дата.
                return marker is APP13 or APP14 or COM;

            default:
                return false;
        }
    }

    /// <summary>
    /// Проверить, содержит ли APP1-сегмент GPS-теги.
    /// </summary>
    private static bool ContainsGpsTag(byte[] input, int app1Pos)
    {
        if (app1Pos + 4 >= input.Length) return false;
        int length = (input[app1Pos + 2] << 8) | input[app1Pos + 3];
        int dataStart = app1Pos + 4;
        int dataEnd = Math.Min(app1Pos + 2 + length, input.Length);

        // Ищем подпись "Exif\0\0"
        if (dataStart + 6 <= dataEnd &&
            input[dataStart] == 0x45 && input[dataStart + 1] == 0x78 &&
            input[dataStart + 2] == 0x69 && input[dataStart + 3] == 0x66 &&
            input[dataStart + 4] == 0x00 && input[dataStart + 5] == 0x00)
        {
            // Ищем GPS-IFD pointer 0x88 0x25
            for (int i = dataStart + 6; i < dataEnd - 1; i++)
            {
                if (input[i] == 0x88 && input[i + 1] == 0x25)
                    return true;
            }

            // Ищем строку "GPS" (в ASCII — если есть теги GPSLatitude и т.д.)
            for (int i = dataStart; i < dataEnd - 3; i++)
            {
                if (input[i] == (byte)'G' && input[i + 1] == (byte)'P' && input[i + 2] == (byte)'S')
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Записать результат в файл (используется извне, если понадобится).
    /// </summary>
    public static void WriteResult(string filePath, byte[] data)
    {
        File.WriteAllBytes(filePath, data);
    }
}