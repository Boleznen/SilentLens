using System;
using SilentLens.Models;

namespace SilentLens.Services;

/// <summary>
/// Обёртка над уведомлениями — учитывает настройку ShowNotifications.
/// </summary>
public sealed class NotificationService
{
    private readonly TrayManager _tray;

    public NotificationService(TrayManager tray)
    {
        _tray = tray;
    }

    public void Show(string title, string message)
    {
        var settings = SettingsManager.Current;
        if (!settings.ShowNotifications) return;

        try
        {
            _tray.ShowNotification(title, message);
        }
        catch { }
    }
}