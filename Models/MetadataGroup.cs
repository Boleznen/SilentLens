using System.Collections.Generic;

namespace SilentLens.Models;

public sealed class MetadataGroup
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = "•";
    public List<MetadataEntry> Entries { get; init; } = new();

    public bool HasGps { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool IsEmpty => Entries.Count == 0;

    public MetadataGroup() { }

    public MetadataGroup(string key, string title, string icon = "•")
    {
        Key = key;
        Title = title;
        Icon = icon;
    }
}