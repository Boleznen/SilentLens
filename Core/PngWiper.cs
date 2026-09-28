using System;
using System.Collections.Generic;
using System.IO;

namespace SilentLens.Core;

public static class PngWiper
{
    // PNG-сигнатура
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static WipeResult Wipe(string filePath, WipeMode mode)
    {
        var result = new WipeResult();
        try
        {
            var original = File.ReadAllBytes(filePath);
            result.OriginalSize = original.Length;

            var output = ProcessPng(original, mode, out var removedCount);

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

    private static byte[] ProcessPng(byte[] input, WipeMode mode, out int removedCount)
    {
        removedCount = 0;

        if (input.Length < 8)
            throw new InvalidDataException("Файл слишком мал для PNG.");

        for (int i = 0; i < 8; i++)
            if (input[i] != PngSignature[i])
                throw new InvalidDataException("Не PNG-файл (нет сигнатуры).");

        var output = new List<byte>(input.Length);
        output.AddRange(PngSignature);

        int pos = 8;

        while (pos + 8 <= input.Length)
        {
            int length = (input[pos] << 24) | (input[pos + 1] << 16) | (input[pos + 2] << 8) | input[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(input, pos + 4, 4);

            int chunkTotal = 8 + length + 4; // длина + тип + данные + CRC
            if (pos + chunkTotal > input.Length)
                break; // битый чанк

            bool shouldRemove = ShouldRemove(type, mode);

            if (shouldRemove)
                removedCount++;
            else
                for (int i = pos; i < pos + chunkTotal; i++)
                    output.Add(input[i]);

            pos += chunkTotal;

            if (type == "IEND") break;
        }

        return output.ToArray();
    }

    private static bool ShouldRemove(string type, WipeMode mode)
    {
        switch (mode)
        {
            case WipeMode.All:
                return type is "tEXt" or "iTXt" or "zTXt" or "eXIf" or "tIME";

            case WipeMode.GpsOnly:
                // В PNG GPS-теги лежат внутри eXIf-чанка (или iTXt с XMP).
                // Удалять их выборочно сложно, поэтому безопаснее удалять весь eXIf.
                return type is "eXIf";

            case WipeMode.KeepDate:
                return type is "tEXt" or "iTXt" or "zTXt" or "eXIf";

            default:
                return false;
        }
    }
}