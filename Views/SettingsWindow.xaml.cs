using System;
using System.Windows;
using System.Windows.Input;
using SilentLens.Services;

namespace SilentLens.Views;

public partial class SettingsWindow : Window
{
    private bool _initializing = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadFromSettings();
        _initializing = false;
    }

    private void LoadFromSettings()
    {
        var s = SettingsManager.Current;

        ChkSaveOriginals.IsChecked = s.SaveOriginals;
        TxtOriginalFolder.Text = s.OriginalFolderName;
        ChkAutoClean.IsChecked = s.AutoCleanBackups;
        TxtBackupDays.Text = s.BackupMaxAgeDays.ToString();

        ChkMinimizeToTray.IsChecked = s.MinimizeToTray;
        ChkAutoStart.IsChecked = StartupManager.IsAutoStartEnabled();
        ChkShowNotifications.IsChecked = s.ShowNotifications;
        ChkSkipBatchConfirm.IsChecked = s.SkipBatchConfirmation;
        ChkCheckUpdates.IsChecked = s.CheckUpdatesOnStartup;
        ChkShellIntegration.IsChecked = ShellIntegration.IsInstalled();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsManager.Current;

        s.SaveOriginals = ChkSaveOriginals.IsChecked == true;
        s.OriginalFolderName = string.IsNullOrWhiteSpace(TxtOriginalFolder.Text)
            ? "_Original"
            : TxtOriginalFolder.Text.Trim();
        s.AutoCleanBackups = ChkAutoClean.IsChecked == true;
        s.BackupMaxAgeDays = ParseInt(TxtBackupDays.Text, 30);

        s.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
        s.AutoStart = ChkAutoStart.IsChecked == true;
        s.ShowNotifications = ChkShowNotifications.IsChecked == true;
        s.SkipBatchConfirmation = ChkSkipBatchConfirm.IsChecked == true;
        s.CheckUpdatesOnStartup = ChkCheckUpdates.IsChecked == true;

        SettingsManager.Save();
        StartupManager.SetAutoStart(s.AutoStart);

        DialogResult = true;
        Close();
    }

    private static int ParseInt(string text, int defaultValue)
    {
        return int.TryParse(text, out var v) && v > 0 ? v : defaultValue;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        var update = await UpdateChecker.CheckAsync();

        if (update == null)
        {
            DarkMessageBox.Show(this,
                $"У вас последняя версия ({UpdateChecker.GetCurrentVersion()}).\n\n" +
                "Если нет соединения с интернетом — обновления не проверяются.",
                "Проверка обновлений",
                DarkMessageIcon.Info,
                DarkMessageButtons.Ok);
            return;
        }

        var result = DarkMessageBox.Show(this,
            $"Доступна новая версия: {update.Version}\n" +
            $"Текущая: {UpdateChecker.GetCurrentVersion()}\n\n" +
            $"Открыть страницу загрузки?",
            "Доступно обновление",
            DarkMessageIcon.Question,
            DarkMessageButtons.YesNo);

        if (result == DarkMessageResult.Yes && !string.IsNullOrEmpty(update.HtmlUrl))
            FolderHelper.OpenUrl(update.HtmlUrl);
    }

    private void ChkSkipBatchConfirm_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        if (ChkSkipBatchConfirm.IsChecked == true)
        {
            var r = DarkMessageBox.Show(this,
                "Отключить подтверждение при пакетной обработке?\n\n" +
                "⚠ ВНИМАНИЕ: если случайно нажать «Применить ко всем», операция\n" +
                "выполнится СРАЗУ, без возможности отменить.\n\n" +
                "Продолжить?",
                "Не спрашивать подтверждение",
                DarkMessageIcon.Warning,
                DarkMessageButtons.YesNo);

            if (r != DarkMessageResult.Yes)
                ChkSkipBatchConfirm.IsChecked = false;
        }
    }

    private void ChkShellIntegration_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        if (ChkShellIntegration.IsChecked == true)
        {
            if (!ShellIntegration.Install())
            {
                DarkMessageBox.Show(this,
                    "Не удалось установить интеграцию.\n" +
                    "Возможно, нет прав на запись в реестр.",
                    "Silent Lens",
                    DarkMessageIcon.Warning,
                    DarkMessageButtons.Ok);
                ChkShellIntegration.IsChecked = false;
            }
        }
        else
        {
            ShellIntegration.Uninstall();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}