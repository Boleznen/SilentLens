namespace SilentLens.Models;

public sealed class MetadataEntry
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public MetadataEntry() { }

    public MetadataEntry(string key, string value)
    {
        Key = key;
        Value = value;
    }
}