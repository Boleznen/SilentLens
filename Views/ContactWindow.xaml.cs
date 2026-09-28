using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace SilentLens.Views;

public partial class ContactWindow : Window
{
    private const string TelegramUrl = "https://t.me/Boleznen";
    private const string GitHubUrl = "https://github.com/Boleznen/SilentLens";
    private const string Email = "boleznen@gmail.com";

    public ContactWindow()
    {
        InitializeComponent();
        EmailText.Text = Email;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OpenTelegram_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(TelegramUrl);
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(GitHubUrl);
    }

    private void CopyEmail_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Email);
            DarkMessageBox.Show(this,
                $"Адрес скопирован:\n{Email}",
                "Silent Lens",
                DarkMessageIcon.Info,
                DarkMessageButtons.Ok);
        }
        catch { }
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show(this,
                $"Не удалось открыть ссылку:\n{ex.Message}",
                "Silent Lens",
                DarkMessageIcon.Warning,
                DarkMessageButtons.Ok);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}