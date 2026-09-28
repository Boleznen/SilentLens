namespace SilentLens.Core;

/// <summary>
/// Режим удаления метаданных.
/// </summary>
public enum WipeMode
{
    /// <summary>Только GPS-координаты.</summary>
    GpsOnly,

    /// <summary>Всё, кроме даты съёмки.</summary>
    KeepDate,

    /// <summary>Все метаданные.</summary>
    All
}

/// <summary>
/// Результат операции удаления.
/// </summary>
public sealed class WipeResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public long OriginalSize { get; set; }
    public long NewSize { get; set; }
    public string? OriginalBackupPath { get; set; }

    public long SavedBytes => OriginalSize - NewSize;
}