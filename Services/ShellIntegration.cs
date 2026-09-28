using System;
using Microsoft.Win32;

namespace SilentLens.Services;

/// <summary>
/// Интеграция в контекстное меню проводника Windows.
/// </summary>
public static class ShellIntegration
{
    private const string KeyPath = @"Software\Classes\*\shell\SilentLens";
    private const string CommandKeyPath = @"Software\Classes\*\shell\SilentLens\command";

    /// <summary>
    /// Установить пункт «Открыть в Silent Lens» в контекстном меню проводника.
    /// </summary>
    public static bool Install()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (key == null) return false;

            key.SetValue("", "Открыть в Silent Lens");
            key.SetValue("Icon", $"\"{exePath}\"");

            using var cmdKey = Registry.CurrentUser.CreateSubKey(CommandKeyPath);
            if (cmdKey == null) return false;

            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Удалить пункт из контекстного меню проводника.
    /// </summary>
    public static bool Uninstall()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsInstalled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }
}