using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SilentLens.Core;
using SilentLens.Models;
using SilentLens.Services;
using SilentLens.Utils;
using SilentLens.Views;

namespace SilentLens;

public partial class MainWindow : Window
{
    public ObservableCollection<PhotoItem> Photos { get; } = new();

    private ICollectionView? _photosView;
    private BatchResult? _lastBatchResult;
    private NotificationService? _notifications;

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = Photos;

        _photosView = CollectionViewSource.GetDefaultView(Photos);
        _photosView.Filter = FilterPhoto;

        SortCombo.SelectedIndex = 0;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;

        Photos.CollectionChanged += (_, _) => UpdateStats();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeMethods.ApplyWorkAreaConstraint(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;

        if (Width > workArea.Width - 40)
            Width = Math.Max(MinWidth, workArea.Width - 40);
        if (Height > workArea.Height - 40)
            Height = Math.Max(MinHeight, workArea.Height - 40);

        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;

        if (Application.Current is App app)
        {
            app.InitTray(this);
            if (app.Tray != null)
                _notifications = new NotificationService(app.Tray);
        }

        var s = SettingsManager.Current;
        if (s.AutoCleanBackups && s.BackupMaxAgeDays > 0)
        {
            _ = Task.Run(() =>
            {
                foreach (var photo in Photos.ToList())
                    BackupCleaner.CleanOldBackups(photo.Directory, s.BackupMaxAgeDays);
            });
        }

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            var files = args.Skip(1).Where(File.Exists).ToArray();
            if (files.Length > 0)
                AddFiles(files);
        }

        UpdateStats();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var s = SettingsManager.Current;
        if (s.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            Application.Current.Shutdown();
        }
    }

    // ============ КЛАВИАТУРА ============

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D)
        {
            BatchApply_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    MenuAddFiles_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D:
                    RunWipe(WipeMode.All);
                    e.Handled = true;
                    break;
                case Key.F:
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                    break;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Delete:
                MenuRemoveSelected_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F5:
                if (FileList.SelectedItem is PhotoItem item)
                {
                    item.MetadataLoaded = false;
                    item.Metadata = null;
                    ShowMetadata(item);
                    ShowPreview(item);
                    e.Handled = true;
                }
                break;
        }
    }

    private void FileList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            MenuRemoveSelected_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ============ DRAG & DROP ============

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = GetDragEffect(e);
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDragEffect(e);
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            var files = new List<string>();
            var folders = new List<string>();

            foreach (var p in paths)
            {
                if (File.Exists(p)) files.Add(p);
                else if (Directory.Exists(p)) folders.Add(p);
            }

            // Файлы добавлены явно — не фильтруем
            if (files.Count > 0) AddFiles(files);

            // Папки — рекурсивный обход, _Original пропускаем
            foreach (var folder in folders)
                AddFolder(folder);

            e.Handled = true;
        }
        catch { }
    }

    private static DragDropEffects GetDragEffect(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return DragDropEffects.None;
        return DragDropEffects.Copy;
    }

    // ============ ФИЛЬТР / СОРТИРОВКА ============

    private bool FilterPhoto(object obj)
    {
        if (obj is not PhotoItem item) return true;

        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return true;

        return item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Directory.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _photosView?.Refresh();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_photosView == null) return;

        var mode = SortCombo.SelectedIndex switch
        {
            1 => SortMode.FileSize,
            2 => SortMode.DateModified,
            3 => SortMode.DateTaken,
            _ => SortMode.FileName
        };

        _photosView.SortDescriptions.Clear();

        switch (mode)
        {
            case SortMode.FileSize:
                _photosView.SortDescriptions.Add(new SortDescription(nameof(PhotoItem.FileSize),
                    ListSortDirection.Ascending));
                break;
            case SortMode.DateModified:
                _photosView.SortDescriptions.Add(new SortDescription(nameof(PhotoItem.FileModified),
                    ListSortDirection.Descending));
                break;
            case SortMode.DateTaken:
                _photosView.SortDescriptions.Add(new SortDescription(nameof(PhotoItem.DateTaken),
                    ListSortDirection.Descending));
                break;
            default:
                _photosView.SortDescriptions.Add(new SortDescription(nameof(PhotoItem.FileName),
                    ListSortDirection.Ascending));
                break;
        }
    }

    // ============ ИМПОРТ ============

    private void MenuAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Выберите фотографии",
            Filter = "Все файлы|*.*"
        };
        if (dlg.ShowDialog(this) == true)
            AddFiles(dlg.FileNames);
    }

    private void MenuAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Выберите папку с фотографиями"
        };
        if (dlg.ShowDialog(this) == true)
            AddFolder(dlg.FolderName);
    }

    private void AddFolder(string folder)
    {
        string[] files;
        try
        {
            files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(f => !IsInOriginalFolder(f))
                .ToArray();
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось прочитать папку:\n{ex.Message}");
            return;
        }

        AddFiles(files);
    }

    /// <summary>Проверить, лежит ли файл в папке _Original (или её подпапке).</summary>
    private static bool IsInOriginalFolder(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, "_Original", StringComparison.OrdinalIgnoreCase))
                return true;

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return false;
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        int added = 0;
        int skipped = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!File.Exists(path)) continue;

            // Проверяем реальный формат по сигнатуре
            var format = FileFormatDetector.Detect(path);
            if (format == ImageFormat.Unknown)
            {
                skipped++;
                continue;
            }

            if (Photos.Any(p => string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var newItem = new PhotoItem(path);
            Photos.Add(newItem);

            _ = LoadThumbnailAndDateAsync(newItem);
            added++;
        }

        if (added > 0)
        {
            StatusText.Text = skipped > 0
                ? $"Добавлено: {added}. Пропущено: {skipped}. Всего: {Photos.Count}"
                : $"Добавлено: {added}. Всего: {Photos.Count}";
        }
        else if (skipped > 0)
        {
            StatusText.Text = $"Пропущено: {skipped}";
        }
        else
        {
            StatusText.Text = "Новых файлов не добавлено";
        }
    }

    private static async Task LoadThumbnailAndDateAsync(PhotoItem item)
    {
        await item.LoadThumbnailAsync();

        if (!item.DateTaken.HasValue && !item.IsReadOnly)
        {
            var date = await Task.Run(() => ThumbnailCache.ReadDateTaken(item.FilePath));
            item.DateTaken = date;
        }
    }

    // ============ ВЫБОР ФАЙЛА ============

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not PhotoItem item)
        {
            PreviewImage.Source = null;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            PreviewPlaceholder.Text = "Выберите файл, чтобы увидеть превью";
            return;
        }

        ShowMetadata(item);
        ShowPreview(item);
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is PhotoItem item)
            FolderHelper.RevealInExplorer(item.FilePath);
    }

    private async void ShowPreview(PhotoItem item)
    {
        PreviewPlaceholder.Visibility = Visibility.Visible;
        PreviewPlaceholder.Text = "Загрузка превью...";
        PreviewImage.Source = null;

        try
        {
            var path = item.FilePath;
            var img = await Task.Run(() => ThumbnailCache.LoadFullImage(path));
            if (img != null)
            {
                PreviewImage.Source = img;
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                PreviewPlaceholder.Text = "Превью недоступно";
                PreviewPlaceholder.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            PreviewPlaceholder.Text = "Не удалось загрузить превью";
            PreviewPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void ShowMetadata(PhotoItem item)
    {
        MetadataPanel.Children.Clear();

        if (!item.MetadataLoaded)
        {
            try
            {
                item.Metadata = MetadataReader.Read(item.FilePath);
                item.MetadataLoaded = true;

                var gps = item.Metadata.FirstOrDefault(g => g.HasGps);
                if (gps != null)
                {
                    item.Latitude = gps.Latitude;
                    item.Longitude = gps.Longitude;
                }

                item.RefreshStatus();
            }
            catch (Exception ex)
            {
                item.Status = PhotoStatus.Error;
                MetadataPanel.Children.Add(new TextBlock
                {
                    Text = $"Ошибка чтения: {ex.Message}",
                    Foreground = (Brush)FindResource("BrushDanger"),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }
        }

        if (item.Metadata == null || item.Metadata.Count == 0)
        {
            MetadataPanel.Children.Add(new TextBlock
            {
                Text = "Метаданные не найдены",
                Foreground = (Brush)FindResource("BrushTextSecondary")
            });
            return;
        }

        MetadataPanel.Children.Add(new TextBlock
        {
            Text = item.FileName,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });

        MetadataPanel.Children.Add(new TextBlock
        {
            Text = item.Directory,
            FontSize = 11,
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var extWithoutDot = item.Extension.TrimStart('.');
        var formatHint = item.ActualFormat switch
        {
            ImageFormat.Jpeg => "jpg",
            ImageFormat.Png => "png",
            ImageFormat.Webp => "webp",
            ImageFormat.Tiff => "tiff",
            ImageFormat.Bmp => "bmp",
            ImageFormat.Gif => "gif",
            ImageFormat.Heic => "heic",
            ImageFormat.Heif => "heif",
            ImageFormat.Avif => "avif",
            _ => ""
        };

        if (!string.IsNullOrEmpty(formatHint) &&
            !extWithoutDot.Equals(formatHint, StringComparison.OrdinalIgnoreCase) &&
            !(extWithoutDot == "jpeg" && formatHint == "jpg") &&
            !(extWithoutDot == "tif" && formatHint == "tiff"))
        {
            MetadataPanel.Children.Add(new Border
            {
                Background = (Brush)FindResource("BrushCard"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = $"ℹ Реальный формат: {item.FormatDisplayName} (расширение .{extWithoutDot})",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("BrushTextSecondary")
                }
            });
        }

        if (item.IsReadOnly)
        {
            MetadataPanel.Children.Add(new Border
            {
                Background = (Brush)FindResource("BrushDanger"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = $"⚠ Формат {item.FormatDisplayName} — только для чтения",
                    FontSize = 11,
                    Foreground = Brushes.White
                }
            });
        }

        if (item.Latitude.HasValue && item.Longitude.HasValue)
        {
            var mapBtn = new Button
            {
                Content = "🗺  Показать на карте",
                Style = (Style)FindResource("AccentButton"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 8),
                Padding = new Thickness(12, 6, 12, 6)
            };
            var lat = item.Latitude.Value;
            var lon = item.Longitude.Value;
            mapBtn.Click += (_, _) =>
            {
                var map = new MapWindow(lat, lon) { Owner = this };
                map.ShowDialog();
            };
            MetadataPanel.Children.Add(mapBtn);
        }

        foreach (var group in item.Metadata)
        {
            if (group.IsEmpty) continue;

            MetadataPanel.Children.Add(new TextBlock
            {
                Text = $"{group.Icon}  {group.Title}",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 6),
                Foreground = (Brush)FindResource("BrushAccent")
            });

            foreach (var entry in group.Entries)
                MetadataPanel.Children.Add(BuildMetadataRow(entry.Key, entry.Value));
        }
    }

    private Grid BuildMetadataRow(string key, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var keyTb = new TextBlock
        {
            Text = key,
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var valTb = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(keyTb, 0);
        Grid.SetColumn(valTb, 1);
        grid.Children.Add(keyTb);
        grid.Children.Add(valTb);
        return grid;
    }

    // ============ УДАЛЕНИЕ ============

    private void WipeGps_Click(object sender, RoutedEventArgs e) => RunWipe(WipeMode.GpsOnly);
    private void WipeKeepDate_Click(object sender, RoutedEventArgs e) => RunWipe(WipeMode.KeepDate);
    private void WipeAll_Click(object sender, RoutedEventArgs e) => RunWipe(WipeMode.All);

    private void RunWipe(WipeMode mode)
    {
        var selected = FileList.SelectedItems.Cast<PhotoItem>().ToList();

        if (selected.Count == 0)
        {
            ShowError("Сначала выберите файл(ы) из списка.");
            return;
        }

        var actionable = selected.Where(i => !i.IsReadOnly).ToList();
        var readOnly = selected.Count - actionable.Count;

        if (actionable.Count == 0)
        {
            ShowError($"Все выбранные файлы ({readOnly}) имеют формат только для чтения.");
            return;
        }

        var modeText = mode switch
        {
            WipeMode.GpsOnly => "GPS-координаты",
            WipeMode.KeepDate => "Всё, кроме даты съёмки",
            WipeMode.All => "Все метаданные",
            _ => mode.ToString()
        };

        var confirm = DarkMessageBox.Show(this,
            $"Удалить: {modeText}\n\n" +
            $"Файлов: {actionable.Count}" +
            (readOnly > 0 ? $" (пропущено только-для-чтения: {readOnly})" : "") +
            "\n\nОригиналы будут сохранены в подпапку «_Original». Продолжить?",
            "Подтверждение",
            DarkMessageIcon.Question,
            DarkMessageButtons.YesNo);

        if (confirm != DarkMessageResult.Yes) return;

        int ok = 0, fail = 0;
        long saved = 0;
        var settings = SettingsManager.Current;

        foreach (var item in actionable)
        {
            var result = WiperService.WipeFile(item.FilePath, mode, saveBackup: settings.SaveOriginals);
            if (result.Success)
            {
                ok++;
                saved += result.SavedBytes;
                item.MetadataLoaded = false;
                item.Metadata = null;
                item.Latitude = null;
                item.Longitude = null;
                item.Status = PhotoStatus.Processed;
            }
            else
            {
                fail++;
                item.Status = PhotoStatus.Error;
            }
        }

        if (FileList.SelectedItem is PhotoItem selectedItem)
        {
            ShowMetadata(selectedItem);
            ShowPreview(selectedItem);
        }

        UpdateStats();

        StatusText.Text = $"Обработано: {ok}. Ошибок: {fail}. Освобождено: {FormatBytes(saved)}";
    }

    // ============ БАТЧ-ОБРАБОТКА ============

    private async void BatchApply_Click(object sender, RoutedEventArgs e)
    {
        if (Photos.Count == 0)
        {
            ShowError("Список файлов пуст. Добавьте файлы для обработки.");
            return;
        }

        var mode = AskBatchMode();
        if (mode == null) return;

        var settings = SettingsManager.Current;

        if (!settings.SkipBatchConfirmation)
        {
            var modeText = mode switch
            {
                WipeMode.GpsOnly => "GPS-координаты",
                WipeMode.KeepDate => "Всё, кроме даты съёмки",
                WipeMode.All => "Все метаданные",
                _ => mode.ToString()
            };

            var confirm = DarkMessageBox.Show(this,
                $"Обработать {Photos.Count} файлов?\n\n" +
                $"Режим: {modeText}\n\n" +
                "Продолжить?",
                "Пакетная обработка",
                DarkMessageIcon.Question,
                DarkMessageButtons.YesNo);

            if (confirm != DarkMessageResult.Yes) return;
        }

        var progressWindow = new BatchProgressWindow { Owner = this };
        var cts = new CancellationTokenSource();
        progressWindow.CancelRequested += (_, _) => cts.Cancel();
        progressWindow.Show();

        TaskbarItemInfo ??= new System.Windows.Shell.TaskbarItemInfo();
        TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

        var progressReporter = new Progress<BatchProgressInfo>(info =>
        {
            progressWindow.UpdateProgress(info);
            if (info.Total > 0)
                TaskbarItemInfo.ProgressValue = (double)info.Current / info.Total;
        });

        var result = await BatchProcessor.ProcessAsync(
            Photos.ToList(),
            mode.Value,
            saveBackup: settings.SaveOriginals,
            progressReporter,
            cts.Token);

        TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        TaskbarItemInfo.ProgressValue = 0;

        _lastBatchResult = result;
        progressWindow.SetFinished();

        foreach (var item in Photos)
        {
            item.MetadataLoaded = false;
            item.Metadata = null;
            item.Latitude = null;
            item.Longitude = null;
            if (item.Status == PhotoStatus.HasMetadata)
                item.Status = PhotoStatus.Processed;
        }

        if (FileList.SelectedItem is PhotoItem selected)
        {
            ShowMetadata(selected);
            ShowPreview(selected);
        }

        UpdateStats();
        ShowBatchReport(result);

        _notifications?.Show("Silent Lens",
            $"Обработано: {result.Processed}. Ошибок: {result.Failed}.");
    }

    private WipeMode? AskBatchMode()
    {
        var r1 = DarkMessageBox.Show(this,
            "Какой режим применить ко всем файлам?\n\n" +
            "Да — только GPS\n" +
            "Нет — все метаданные\n" +
            "Отмена — ничего не делать",
            "Пакетная обработка",
            DarkMessageIcon.Question,
            DarkMessageButtons.YesNoCancel);

        if (r1 == DarkMessageResult.Cancel) return null;
        if (r1 == DarkMessageResult.Yes) return WipeMode.GpsOnly;
        return WipeMode.All;
    }

    private void ShowBatchReport(BatchResult result)
    {
        var report =
            $"Пакетная обработка завершена.\n\n" +
            $"Всего файлов: {result.Total}\n" +
            $"Успешно обработано: {result.Processed}\n" +
            $"Пропущено (только чтение): {result.Skipped}\n" +
            $"Ошибок: {result.Failed}\n" +
            $"Освобождено: {FormatBytes(result.SavedBytes)}\n" +
            (result.Cancelled ? "\n⚠ Операция была отменена." : "");

        DarkMessageBox.Show(this, report, "Отчёт",
            DarkMessageIcon.Info, DarkMessageButtons.Ok);
    }

    // ============ ЭКСПОРТ ============

    private void MenuExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastBatchResult == null || _lastBatchResult.Items.Count == 0)
        {
            ShowError("Нет отчёта для экспорта. Сначала выполните пакетную обработку.");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Сохранить отчёт",
            Filter = "Текстовый файл (*.txt)|*.txt|CSV (*.csv)|*.csv",
            FileName = $"SilentLens_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var isCsv = dlg.FilterIndex == 2;
            var content = isCsv
                ? BuildCsvReport(_lastBatchResult)
                : BuildTxtReport(_lastBatchResult);

            File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
            StatusText.Text = $"Отчёт сохранён: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось сохранить отчёт:\n{ex.Message}");
        }
    }

    private void MenuExportMetaTxt_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileList.SelectedItems.Cast<PhotoItem>().ToList();
        if (selected.Count == 0)
        {
            ShowError("Сначала выберите файл(ы) из списка.");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Экспорт метаданных в TXT",
            Filter = "Текстовый файл (*.txt)|*.txt",
            FileName = $"SilentLens_metadata_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var content = MetadataExporter.ExportTxt(selected);
            File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
            StatusText.Text = $"Экспорт завершён: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
        }
    }

    private void MenuExportMetaJson_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileList.SelectedItems.Cast<PhotoItem>().ToList();
        if (selected.Count == 0)
        {
            ShowError("Сначала выберите файл(ы) из списка.");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Экспорт метаданных в JSON",
            Filter = "JSON (*.json)|*.json",
            FileName = $"SilentLens_metadata_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var content = MetadataExporter.ExportJson(selected);
            File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
            StatusText.Text = $"Экспорт завершён: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
        }
    }

    private void MenuExportOriginalsZip_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileList.SelectedItems.Cast<PhotoItem>().ToList();
        if (selected.Count == 0)
            selected = Photos.ToList();

        if (selected.Count == 0)
        {
            ShowError("Нет файлов в списке.");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Экспорт оригиналов в ZIP",
            Filter = "ZIP-архив (*.zip)|*.zip",
            FileName = $"SilentLens_originals_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
        };

        if (dlg.ShowDialog(this) != true) return;

        var (added, zipPath) = ArchiveService.ArchiveOriginals(selected, dlg.FileName);

        if (added > 0)
        {
            StatusText.Text = $"Экспортировано {added} файлов в {zipPath}";
            DarkMessageBox.Show(this,
                $"В архив добавлено файлов: {added}\n\n{zipPath}",
                "Экспорт оригиналов",
                DarkMessageIcon.Info,
                DarkMessageButtons.Ok);
        }
        else
        {
            ShowError("Не найдено ни одного оригинала в _Original.");
        }
    }

    private static string BuildTxtReport(BatchResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Silent Lens — отчёт о пакетной обработке");
        sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Всего файлов: {r.Total}");
        sb.AppendLine($"Успешно: {r.Processed}");
        sb.AppendLine($"Пропущено: {r.Skipped}");
        sb.AppendLine($"Ошибок: {r.Failed}");
        sb.AppendLine($"Освобождено: {FormatBytes(r.SavedBytes)}");
        sb.AppendLine();

        if (r.Items.Count > 0)
        {
            sb.AppendLine("Детали по файлам:");
            sb.AppendLine(new string('─', 80));
            foreach (var it in r.Items)
            {
                sb.AppendLine($"[{it.Status}] {it.FileName}");
                if (!string.IsNullOrEmpty(it.Message))
                    sb.AppendLine($"    {it.Message}");
                if (it.SavedBytes > 0)
                    sb.AppendLine($"    Освобождено: {FormatBytes(it.SavedBytes)}");
                if (!string.IsNullOrEmpty(it.BackupPath))
                    sb.AppendLine($"    Оригинал: {it.BackupPath}");
            }
        }

        return sb.ToString();
    }

    private static string BuildCsvReport(BatchResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("File,Status,SavedBytes,Message,BackupPath");

        foreach (var it in r.Items)
        {
            sb.Append(EscapeCsv(it.FileName)).Append(',');
            sb.Append(EscapeCsv(it.Status)).Append(',');
            sb.Append(it.SavedBytes).Append(',');
            sb.Append(EscapeCsv(it.Message ?? "")).Append(',');
            sb.AppendLine(EscapeCsv(it.BackupPath ?? ""));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ============ СРАВНЕНИЕ ============

    private void MenuCompare_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not PhotoItem item)
        {
            ShowError("Сначала выберите файл из списка.");
            return;
        }

        var w = new CompareWindow(item) { Owner = this };
        w.ShowDialog();
    }

    // ============ СТАТИСТИКА ============

    private void UpdateStats()
    {
        int total = Photos.Count;
        int readOnly = Photos.Count(p => p.IsReadOnly);
        int processed = Photos.Count(p => p.Status == PhotoStatus.Processed);
        int withMeta = Photos.Count(p => p.Status == PhotoStatus.HasMetadata);
        int noMeta = Photos.Count(p => p.Status == PhotoStatus.NoMetadata);

        StatusStats.Text = total == 0
            ? ""
            : $"Всего: {total}  ·  Метаданные: {withMeta}  ·  Пусто: {noMeta}  ·  Обработано: {processed}  ·  Только чтение: {readOnly}";
    }

    // ============ МЕНЮ ============

    private void MenuRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileList.SelectedItems.Cast<PhotoItem>().ToList();
        foreach (var item in selected)
            Photos.Remove(item);

        UpdateStats();
    }

    private void MenuClearList_Click(object sender, RoutedEventArgs e)
    {
        Photos.Clear();
        MetadataPanel.Children.Clear();
        PreviewImage.Source = null;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        StatusText.Text = "Список очищен";
        UpdateStats();
    }

    private void MenuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not PhotoItem item) return;

        try
        {
            Clipboard.SetText(item.FilePath);
            StatusText.Text = "Путь скопирован";
        }
        catch { }
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow { Owner = this };
        w.ShowDialog();
    }

    private async void MenuCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Проверка обновлений...";

        var update = await UpdateChecker.CheckAsync();

        if (update == null)
        {
            StatusText.Text = "Обновлений не найдено";
            DarkMessageBox.Show(this,
                $"У вас последняя версия ({UpdateChecker.GetCurrentVersion()}).\n\n" +
                "Если нет соединения с интернетом — обновления не проверяются.",
                "Проверка обновлений",
                DarkMessageIcon.Info,
                DarkMessageButtons.Ok);
            return;
        }

        StatusText.Text = $"Доступна версия {update.Version}";

        var result = DarkMessageBox.Show(this,
            $"Доступна новая версия: {update.Version}\n" +
            $"Текущая: {UpdateChecker.GetCurrentVersion()}\n\n" +
            $"Открыть страницу загрузки?",
            "Доступно обновление",
            DarkMessageIcon.Question,
            DarkMessageButtons.YesNo);

        if (result == DarkMessageResult.Yes && !string.IsNullOrEmpty(update.HtmlUrl))
        {
            FolderHelper.OpenUrl(update.HtmlUrl);
        }
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var w = new AboutWindow { Owner = this };
        w.ShowDialog();
    }

    private void MenuContact_Click(object sender, RoutedEventArgs e)
    {
        var w = new ContactWindow { Owner = this };
        w.ShowDialog();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    // ============ ПАПКИ ============

    private void BtnFolders_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void BtnOpenFileFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not PhotoItem item)
        {
            ShowError("Сначала выберите файл из списка.");
            return;
        }
        FolderHelper.RevealInExplorer(item.FilePath);
    }

    private void MenuOpenFileFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not PhotoItem item)
        {
            ShowError("Сначала выберите файл из списка.");
            return;
        }
        FolderHelper.RevealInExplorer(item.FilePath);
    }

    private void MenuOpenOriginalFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not PhotoItem item)
        {
            ShowError("Сначала выберите файл из списка.");
            return;
        }

        var folder = FolderHelper.GetOriginalFolder(item.FilePath);

        if (!Directory.Exists(folder))
        {
            DarkMessageBox.Show(this,
                $"Папка с оригиналом ещё не создана.\n\n" +
                $"Ожидаемый путь:\n{folder}\n\n" +
                "Оригиналы сохраняются сюда при удалении метаданных.",
                "Silent Lens",
                DarkMessageIcon.Info,
                DarkMessageButtons.Ok);
            return;
        }

        FolderHelper.OpenInExplorer(folder);
    }

    private void MenuOpenLogs_Click(object sender, RoutedEventArgs e)
        => FolderHelper.OpenInExplorer(FolderHelper.GetLogsFolder());

    private void MenuOpenSettingsFolder_Click(object sender, RoutedEventArgs e)
        => FolderHelper.OpenInExplorer(FolderHelper.GetSettingsFolder());

    private void MenuOpenDataFolder_Click(object sender, RoutedEventArgs e)
        => FolderHelper.OpenInExplorer(FolderHelper.GetDataFolder());

    private void MenuOpenSettingsFile_Click(object sender, RoutedEventArgs e)
    {
        var file = FolderHelper.GetSettingsFile();

        if (!File.Exists(file))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, "{\n}\n", Encoding.UTF8);
            }
            catch { }
        }

        if (File.Exists(file))
            FolderHelper.RevealInExplorer(file);
        else
            DarkMessageBox.Show(this,
                $"Файл настроек ещё не создан.\n\nОжидаемый путь:\n{file}",
                "Silent Lens", DarkMessageIcon.Info, DarkMessageButtons.Ok);
    }

    // ============ ТАЙТЛБАР ============

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
        else if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // ============ ХЕЛПЕР ============

    private void ShowError(string message)
    {
        DarkMessageBox.Show(this, message, "Silent Lens",
            DarkMessageIcon.Warning, DarkMessageButtons.Ok);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} Б";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} КБ";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.##} МБ";
        return $"{bytes / (1024.0 * 1024 * 1024):0.##} ГБ";
    }
}