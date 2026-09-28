using System.Collections.Generic;

namespace SilentLens.Models;

public sealed class BatchResult
{
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public long SavedBytes { get; set; }
    public bool Cancelled { get; set; }

    public List<BatchItemResult> Items { get; set; } = new();
}

public sealed class BatchItemResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "OK", "Skipped", "Failed"
    public string? Message { get; set; }
    public long SavedBytes { get; set; }
    public string? BackupPath { get; set; }
}