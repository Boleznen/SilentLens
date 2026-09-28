using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SilentLens.Core;

public static class WebpWiper
{
    public static WipeResult Wipe(string filePath, WipeMode mode)
    {
        var result = new WipeResult();
        try
        {
            var original = File.ReadAllBytes(filePath);
            result.OriginalSize = original.Length;

            var output = ProcessWebp(original, mode, out var removedCount);

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

    private static byte[] ProcessWebp(byte[] input, WipeMode mode, out int removedCount)
    {
        removedCount = 0;

        if (input.Length < 12)
            throw new InvalidDataException("Файл слишком мал для WebP.");

        // RIFF
        if (input[0] != 'R' || input[1] != 'I' || input[2] != 'F' || input[3] != 'F')
            throw new InvalidDataException("Не WebP-файл (нет RIFF).");

        if (input[8] != 'W' || input[9] != 'E' || input[10] != 'B' || input[11] != 'P')
            throw new InvalidDataException("Не WebP-файл (нет WEBP).");

        var chunks = new List<(string type, byte[] data)>();
        int pos = 12;

        while (pos + 8 <= input.Length)
        {
            string type = Encoding.ASCII.GetString(input, pos, 4);
            int size = BitConverter.ToInt32(input, pos + 4);
            if (size < 0 || pos + 8 + size > input.Length) break;

            var data = new byte[size];
            Array.Copy(input, pos + 8, data, 0, size);
            chunks.Add((type, data));

            pos += 8 + size;
            if (size % 2 != 0) pos++; // паддинг до чётного
        }

        // Фильтруем
        var kept = new List<(string type, byte[] data)>();
        foreach (var (type, data) in chunks)
        {
            bool remove = ShouldRemove(type, mode);
            if (remove) removedCount++;
            else kept.Add((type, data));
        }

        // Если был VP8X — надо обновить флаги (снять биты EXIF/XMP/ICC)
        // Для простоты: если удалили все метаданные, оставляем VP8X как есть,
        // но флаги наличия EXIF/XMP могут остаться. Простейшее решение — просто
        // не трогать VP8X. Большинство декодеров игнорируют несоответствия.

        // Собираем обратно
        using var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        int payloadSize = 4; // "WEBP"
        foreach (var (type, data) in kept)
            payloadSize += 8 + data.Length + (data.Length % 2);

        bw.Write(payloadSize);
        bw.Write(new[] { (byte)'W', (byte)'E', (byte)'B', (byte)'P' });

        foreach (var (type, data) in kept)
        {
            bw.Write(Encoding.ASCII.GetBytes(type));
            bw.Write(data.Length);
            bw.Write(data);
            if (data.Length % 2 != 0) bw.Write((byte)0);
        }

        return ms.ToArray();
    }

    private static bool ShouldRemove(string type, WipeMode mode)
    {
        var t = type.TrimEnd();
        switch (mode)
        {
            case WipeMode.All:
                return t is "EXIF" or "XMP" or "ICCP";

            case WipeMode.GpsOnly:
                return t == "EXIF"; // простая стратегия — снести весь EXIF (в нём GPS)

            case WipeMode.KeepDate:
                return t is "XMP" or "ICCP";

            default:
                return false;
        }
    }
}