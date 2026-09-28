using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace SilentLens.Services;

public sealed class TrayManager : IDisposable
{
    private TaskbarIcon? _tray;
    private Window? _mainWindow;

    public event EventHandler? ExitRequested;

    /// <summary>Инициализировать иконку в трее.</summary>
    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        _tray = new TaskbarIcon
        {
            ToolTipText = "Silent Lens — управление метаданными фото",
            Visibility = Visibility.Visible
        };

        _tray.Icon = LoadTrayIcon();

        // Контекстное меню
        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = "Показать окно" };
        showItem.Click += (_, _) => RestoreMainWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Выход" };
        exitItem.Click += (_, _) =>
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        };
        menu.Items.Add(exitItem);

        _tray.ContextMenu = menu;

        // Двойной клик — восстановить окно
        _tray.TrayMouseDoubleClick += (_, _) => RestoreMainWindow();
    }

    /// <summary>
    /// Загрузить иконку для трея. Приоритет:
    /// 1. WPF-ресурс pack://application:,,,/Assets/app.ico (вшит в exe).
    /// 2. Иконка, извлечённая из exe (та же, что в ApplicationIcon).
    /// 3. Стандартная системная (на крайний случай).
    /// </summary>
    private static Icon? LoadTrayIcon()
    {
        // Способ 1: из WPF-ресурса
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo?.Stream != null)
            {
                using var stream = streamInfo.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
            // пробуем следующий способ
        }

        // Способ 2: извлечь из exe (иконка вшита через <ApplicationIcon>)
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(exePath);
                if (extracted != null) return extracted;
            }
        }
        catch
        {
            // игнорируем
        }

        // Способ 3: fallback — системная
        return SystemIcons.Shield;
    }

    private void RestoreMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>Показать уведомление в трее.</summary>
    public void ShowNotification(string title, string message)
    {
        try
        {
            _tray?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }
        catch { }
    }

    public void Dispose()
    {
        try
        {
            _tray?.Dispose();
        }
        catch { }
    }
}