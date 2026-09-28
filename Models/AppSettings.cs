using System;

namespace SilentLens.Models;

public enum SortMode
{
    FileName,
    FileSize,
    DateModified,
    DateTaken
}

public sealed class AppSettings
{
    // === Основные ===
    public bool SaveOriginals { get; set; } = true;
    public string OriginalFolderName { get; set; } = "_Original";

    // === Поведение окна ===
    public bool MinimizeToTray { get; set; } = false;
    public bool AutoStart { get; set; } = false;

    // === Обновления ===
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public DateTime? LastUpdateCheck { get; set; }
    public string? SkippedVersion { get; set; }

    // === Уведомления ===
    public bool ShowNotifications { get; set; } = true;

    // === Пакетная обработка ===
    public bool SkipBatchConfirmation { get; set; } = false;

    // === Авто-очистка бэкапов ===
    public bool AutoCleanBackups { get; set; } = false;
    public int BackupMaxAgeDays { get; set; } = 30;

    // === Сортировка ===
    public SortMode DefaultSort { get; set; } = SortMode.FileName;
    public bool SortAscending { get; set; } = true;
}