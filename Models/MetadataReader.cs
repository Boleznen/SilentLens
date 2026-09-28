using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using SilentLens.Models;

namespace SilentLens.Core;

public static class MetadataReader
{
    public static List<MetadataGroup> Read(string filePath)
    {
        var groups = new List<MetadataGroup>();

        IReadOnlyList<MetadataExtractor.Directory> directories;
        try
        {
            directories = ImageMetadataReader.ReadMetadata(filePath);
        }
        catch (Exception ex)
        {
            var errorGroup = new MetadataGroup("Error", "Ошибка чтения", "⚠");
            errorGroup.Entries.Add(new MetadataEntry("Ошибка", ex.Message));
            groups.Add(errorGroup);
            return groups;
        }

        var camera = new MetadataGroup("Camera", "Камера", "📷");
        CollectFrom(directories, camera,
            ExifDirectoryBase.TagMake,
            ExifDirectoryBase.TagModel,
            ExifDirectoryBase.TagSoftware,
            ExifDirectoryBase.TagHostComputer);
        if (!camera.IsEmpty) groups.Add(camera);

        var date = new MetadataGroup("Date", "Дата и время", "⏱");
        CollectFrom(directories, date,
            ExifDirectoryBase.TagDateTimeOriginal,
            ExifDirectoryBase.TagDateTimeDigitized,
            ExifDirectoryBase.TagDateTime);
        if (!date.IsEmpty) groups.Add(date);

        var shot = new MetadataGroup("Shot", "Параметры съёмки", "🎛");
        CollectFrom(directories, shot,
            ExifDirectoryBase.TagIsoEquivalent,
            ExifDirectoryBase.TagExposureTime,
            ExifDirectoryBase.TagFNumber,
            ExifDirectoryBase.TagFocalLength,
            ExifDirectoryBase.TagExposureProgram,
            ExifDirectoryBase.TagFlash);
        if (!shot.IsEmpty) groups.Add(shot);

        var gps = ReadGps(directories);
        if (gps != null) groups.Add(gps);

        var iptc = new MetadataGroup("Iptc", "IPTC", "📝");
        CollectIptc(directories, iptc);
        if (!iptc.IsEmpty) groups.Add(iptc);

        var other = new MetadataGroup("Other", "Прочее", "⚙");
        CollectFrom(directories, other,
            ExifDirectoryBase.TagOrientation,
            ExifDirectoryBase.TagXResolution,
            ExifDirectoryBase.TagYResolution,
            ExifDirectoryBase.TagResolutionUnit,
            ExifDirectoryBase.TagColorSpace);
        if (!other.IsEmpty) groups.Add(other);

        return groups;
    }

    private static void CollectFrom(
        IReadOnlyList<MetadataExtractor.Directory> dirs,
        MetadataGroup group,
        params int[] tags)
    {
        foreach (var dir in dirs)
        {
            foreach (var tag in tags)
            {
                if (dir.ContainsTag(tag))
                {
                    var desc = dir.GetDescription(tag);
                    if (!string.IsNullOrWhiteSpace(desc) && desc != "Unknown")
                    {
                        var name = dir.GetTagName(tag) ?? $"Tag 0x{tag:X}";
                        if (group.Entries.All(e => e.Key != name))
                            group.Entries.Add(new MetadataEntry(name, desc));
                    }
                }
            }
        }
    }

    private static void CollectIptc(
        IReadOnlyList<MetadataExtractor.Directory> dirs,
        MetadataGroup group)
    {
        foreach (var dir in dirs.OfType<IptcDirectory>())
        {
            AddIptc(dir, IptcDirectory.TagByLine, "Автор", group);
            AddIptc(dir, IptcDirectory.TagCopyrightNotice, "Copyright", group);
            AddIptc(dir, IptcDirectory.TagCaption, "Описание", group);
            AddIptc(dir, IptcDirectory.TagKeywords, "Ключевые слова", group);
            AddIptc(dir, IptcDirectory.TagCity, "Город", group);
        }
    }

    private static void AddIptc(
        IptcDirectory dir, int tag, string name, MetadataGroup group)
    {
        if (dir.ContainsTag(tag))
        {
            var desc = dir.GetDescription(tag);
            if (!string.IsNullOrWhiteSpace(desc))
                group.Entries.Add(new MetadataEntry(name, desc));
        }
    }

    private static MetadataGroup? ReadGps(IReadOnlyList<MetadataExtractor.Directory> dirs)
    {
        var gpsDir = dirs.OfType<GpsDirectory>().FirstOrDefault();
        if (gpsDir == null) return null;

        bool hasLat = gpsDir.ContainsTag(GpsDirectory.TagLatitude);
        bool hasLon = gpsDir.ContainsTag(GpsDirectory.TagLongitude);
        if (!hasLat && !hasLon) return null;

        var group = new MetadataGroup("Gps", "GPS", "🛰") { HasGps = true };

        if (hasLat)
        {
            var latDesc = gpsDir.GetDescription(GpsDirectory.TagLatitude);
            group.Entries.Add(new MetadataEntry("Широта", latDesc ?? "—"));
        }

        if (hasLon)
        {
            var lonDesc = gpsDir.GetDescription(GpsDirectory.TagLongitude);
            group.Entries.Add(new MetadataEntry("Долгота", lonDesc ?? "—"));
        }

        if (gpsDir.ContainsTag(GpsDirectory.TagAltitude))
        {
            var altDesc = gpsDir.GetDescription(GpsDirectory.TagAltitude);
            group.Entries.Add(new MetadataEntry("Высота", altDesc ?? "—"));
        }

        try
        {
            var loc = gpsDir.GetGeoLocation();
            if (loc != null)
            {
                var latProp = loc.GetType().GetProperty("Latitude");
                var lonProp = loc.GetType().GetProperty("Longitude");
                if (latProp != null && lonProp != null)
                {
                    var latVal = latProp.GetValue(loc);
                    var lonVal = lonProp.GetValue(loc);
                    if (latVal is double dLat) group.Latitude = dLat;
                    if (lonVal is double dLon) group.Longitude = dLon;

                    if (group.Latitude.HasValue && group.Longitude.HasValue)
                    {
                        group.Entries.Add(new MetadataEntry("Координаты",
                            $"{group.Latitude.Value:0.######}, {group.Longitude.Value:0.######}"));
                    }
                }
            }
        }
        catch { }

        return group;
    }
}