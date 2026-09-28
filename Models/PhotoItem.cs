using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using SilentLens.Core;

namespace SilentLens.Models;

public enum PhotoStatus
{
    Unknown,
    HasMetadata,
    NoMetadata,
    Processed,
    ReadOnly,
    Error
}

public sealed class PhotoItem : INotifyPropertyChanged
{
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();
    public long FileSize { get; private set; }
    public DateTime FileModified { get; private set; }

    /// <summary>Реальный формат файла (по сигнатуре).</summary>
    public ImageFormat ActualFormat { get; private set; } = ImageFormat.Unknown;

    /// <summary>Человекочитаемое имя формата.</summary>
    public string FormatDisplayName => FileFormatDetector.GetDisplayName(ActualFormat);

    private DateTime? _dateTaken;
    public DateTime? DateTaken
    {
        get => _dateTaken;
        set { _dateTaken = value; OnPropertyChanged(); }
    }

    private bool _isReadOnly;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set { _isReadOnly = value; OnPropertyChanged(); }
    }

    private List<MetadataGroup>? _metadata;
    public List<MetadataGroup>? Metadata
    {
        get => _metadata;
        set { _metadata = value; OnPropertyChanged(); }
    }

    private bool _metadataLoaded;
    public bool MetadataLoaded
    {
        get => _metadataLoaded;
        set { _metadataLoaded = value; OnPropertyChanged(); }
    }

    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    private double? _latitude;
    public double? Latitude
    {
        get => _latitude;
        set { _latitude = value; OnPropertyChanged(); }
    }

    private double? _longitude;
    public double? Longitude
    {
        get => _longitude;
        set { _longitude = value; OnPropertyChanged(); }
    }

    private PhotoStatus _status = PhotoStatus.Unknown;
    public PhotoStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public string StatusIcon => _status switch
    {
        PhotoStatus.HasMetadata => "●",
        PhotoStatus.NoMetadata => "○",
        PhotoStatus.Processed => "✓",
        PhotoStatus.ReadOnly => "🔒",
        PhotoStatus.Error => "⚠",
        _ => "·"
    };

    public Brush StatusBrush => _status switch
    {
        PhotoStatus.HasMetadata => new SolidColorBrush(Color.FromRgb(0x3D, 0x7E, 0xBF)),
        PhotoStatus.NoMetadata => new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70)),
        PhotoStatus.Processed => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        PhotoStatus.ReadOnly => new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)),
        PhotoStatus.Error => new SolidColorBrush(Color.FromRgb(0xB0, 0x30, 0x3A)),
        _ => new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x70))
    };

    public string FileSizeFormatted => FormatSize(FileSize);

    public PhotoItem(string filePath)
    {
        FilePath = filePath;

        try
        {
            var fi = new FileInfo(filePath);
            if (fi.Exists)
            {
                FileSize = fi.Length;
                FileModified = fi.LastWriteTime;
            }
        }
        catch { }

        // Определяем реальный формат по сигнатуре
        ActualFormat = FileFormatDetector.Detect(filePath);

        // Формат только для чтения?
        IsReadOnly = !FileFormatDetector.IsWritable(ActualFormat);

        if (IsReadOnly)
            Status = PhotoStatus.ReadOnly;
    }

    /// <summary>Асинхронно загрузить миниатюру.</summary>
    public async Task LoadThumbnailAsync()
    {
        if (Thumbnail != null) return;

        var path = FilePath;
        var img = await Task.Run(() => ThumbnailCache.GetOrLoad(path)).ConfigureAwait(true);
        if (img != null) Thumbnail = img;
    }

    /// <summary>Пересчитать статус по факту наличия метаданных.</summary>
    public void RefreshStatus()
    {
        if (IsReadOnly)
        {
            Status = PhotoStatus.ReadOnly;
            return;
        }

        if (Metadata == null || Metadata.Count == 0)
        {
            Status = PhotoStatus.NoMetadata;
            return;
        }

        Status = PhotoStatus.HasMetadata;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} Б";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} КБ";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.##} МБ";
        return $"{bytes / (1024.0 * 1024 * 1024):0.##} ГБ";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}