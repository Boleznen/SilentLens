using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SilentLens.Core;
using SilentLens.Models;
using SilentLens.Services;

namespace SilentLens.Views;

public partial class CompareWindow : Window
{
    private readonly PhotoItem _item;
    private readonly string? _backupPath;
    private List<MetadataGroup>? _beforeMetadata;
    private List<MetadataGroup>? _afterMetadata;

    public CompareWindow(PhotoItem item)
    {
        InitializeComponent();
        _item = item;

        var origFolder = FolderHelper.GetOriginalFolder(item.FilePath);
        var candidateBackup = Path.Combine(origFolder, Path.GetFileName(item.FilePath));

        _backupPath = File.Exists(candidateBackup) ? candidateBackup : null;

        Loaded += async (_, _) => await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (_backupPath != null)
        {
            _beforeMetadata = await System.Threading.Tasks.Task.Run(
                () => MetadataReader.Read(_backupPath));
            ShowGroups(BeforePanel, _beforeMetadata, isBefore: true);
        }
        else
        {
            BeforePanel.Children.Add(new TextBlock
            {
                Text = "Оригинал не найден в _Original.\nВозможно, он был удалён или перенесён.",
                Foreground = (Brush)FindResource("BrushTextSecondary"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        _afterMetadata = await System.Threading.Tasks.Task.Run(
            () => MetadataReader.Read(_item.FilePath));
        ShowGroups(AfterPanel, _afterMetadata, isBefore: false);
    }

    private void ShowGroups(StackPanel panel, List<MetadataGroup>? groups, bool isBefore)
    {
        panel.Children.Clear();

        if (groups == null || groups.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Метаданные отсутствуют",
                Foreground = (Brush)FindResource("BrushTextSecondary")
            });
            return;
        }

        foreach (var g in groups)
        {
            if (g.IsEmpty) continue;

            var header = new TextBlock
            {
                Text = $"{g.Icon}  {g.Title}",
                FontWeight = FontWeights.SemiBold,
                Foreground = isBefore
                    ? (Brush)FindResource("BrushAccent")
                    : (Brush)FindResource("BrushSuccess"),
                Margin = new Thickness(0, 12, 0, 6)
            };

            if (!isBefore)
            {
                var other = _beforeMetadata;
                if (other != null && !other.Any(x => x.Key == g.Key))
                {
                    header.Text = "✓  " + header.Text;
                }
            }

            panel.Children.Add(header);

            foreach (var e in g.Entries)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var key = new TextBlock
                {
                    Text = e.Key,
                    Foreground = (Brush)FindResource("BrushTextSecondary"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var val = new TextBlock
                {
                    Text = e.Value,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top
                };

                Grid.SetColumn(key, 0);
                Grid.SetColumn(val, 1);
                grid.Children.Add(key);
                grid.Children.Add(val);
                panel.Children.Add(grid);
            }
        }

        // Сводка — безопасный подсчёт без null
        int count = 0;
        foreach (var g in groups)
            count += g?.Entries?.Count ?? 0;

        var summary = new Border
        {
            Background = (Brush)FindResource("BrushCard"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 12, 0, 0)
        };

        summary.Child = new TextBlock
        {
            Text = $"Всего записей: {count}",
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            FontSize = 11
        };
        panel.Children.Add(summary);
    }

    private void BtnExportTxt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Сохранить сравнение",
            Filter = "Текстовый файл (*.txt)|*.txt",
            FileName = $"SilentLens_compare_{_item.FileName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Сравнение метаданных: {_item.FileName}");
            sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Оригинал: {_backupPath ?? "(не найден)"}");
            sb.AppendLine();

            sb.AppendLine("========== ДО ==========");
            AppendGroups(sb, _beforeMetadata);

            sb.AppendLine();
            sb.AppendLine("========== ПОСЛЕ ==========");
            AppendGroups(sb, _afterMetadata);

            File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            DarkMessageBox.Show(this, "Экспорт завершён.", "Сравнение",
                DarkMessageIcon.Info, DarkMessageButtons.Ok);
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show(this, $"Ошибка: {ex.Message}", "Сравнение",
                DarkMessageIcon.Error, DarkMessageButtons.Ok);
        }
    }

    private void BtnExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Сохранить сравнение (JSON)",
            Filter = "JSON (*.json)|*.json",
            FileName = $"SilentLens_compare_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var obj = new
            {
                fileName = _item.FileName,
                filePath = _item.FilePath,
                backupPath = _backupPath,
                exportedAt = DateTime.Now,
                before = _beforeMetadata,
                after = _afterMetadata
            };

            var json = System.Text.Json.JsonSerializer.Serialize(obj,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
            DarkMessageBox.Show(this, "Экспорт завершён.", "Сравнение",
                DarkMessageIcon.Info, DarkMessageButtons.Ok);
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show(this, $"Ошибка: {ex.Message}", "Сравнение",
                DarkMessageIcon.Error, DarkMessageButtons.Ok);
        }
    }

    private static void AppendGroups(System.Text.StringBuilder sb,
        List<MetadataGroup>? groups)
    {
        if (groups == null) return;

        foreach (var g in groups)
        {
            if (g.IsEmpty) continue;
            sb.AppendLine($"[{g.Title}]");
            foreach (var e in g.Entries)
                sb.AppendLine($"  {e.Key}: {e.Value}");
            sb.AppendLine();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}