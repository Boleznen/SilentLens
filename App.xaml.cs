using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using SilentLens.Services;

namespace SilentLens;

public partial class App : Application
{
    private TrayManager? _tray;

    public TrayManager? Tray => _tray;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            // Загружаем настройки
            var settings = SettingsManager.Current;

            // Инициализируем трей
            _tray = new TrayManager();
            _tray.ExitRequested += (_, _) =>
            {
                _tray?.Dispose();
                Shutdown();
            };

            // Проверка обновлений при запуске (тихо, в фоне)
            if (settings.CheckUpdatesOnStartup)
            {
                _ = CheckUpdatesInBackgroundAsync();
            }
        }
        catch (Exception ex)
        {
            ShowFatal("OnStartup", ex);
            Shutdown(1);
        }
    }

    private async System.Threading.Tasks.Task CheckUpdatesInBackgroundAsync()
    {
        try
        {
            // Небольшая задержка, чтобы окно успело прогрузиться
            await System.Threading.Tasks.Task.Delay(3000);

            var update = await UpdateChecker.CheckAsync();
            if (update == null) return;

            // Показываем ненавязчиво через трей
            _tray?.ShowNotification(
                "Доступно обновление",
                $"Silent Lens {update.Version} — откройте Настройки для загрузки.");
        }
        catch { }
    }

    /// <summary>Инициализировать трей (вызывается после открытия главного окна).</summary>
    public void InitTray(Window mainWindow)
    {
        _tray?.Initialize(mainWindow);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatal("Dispatcher", e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogFatal("AppDomain", ex);
    }

    private static void ShowFatal(string source, Exception ex)
    {
        LogFatal(source, ex);
        try
        {
            MessageBox.Show(
                $"Непредвиденная ошибка ({source}):\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                $"Лог: {GetLogPath()}",
                "Silent Lens — ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    private static void LogFatal(string source, Exception ex)
    {
        try
        {
            var logPath = GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder();
            sb.AppendLine($"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] =====");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }

    private static string GetLogPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "SilentLens", "logs", "fatal.log");
    }
}