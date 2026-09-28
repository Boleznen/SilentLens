using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SilentLens.Services;

namespace SilentLens.Views;

public partial class AboutWindow : Window
{
    private const string Email = "boleznen@gmail.com";

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "v" + UpdateChecker.GetCurrentVersion();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Telegram_Click(object sender, MouseButtonEventArgs e)
    {
        OpenContactWindow();
    }

    private void Email_Click(object sender, MouseButtonEventArgs e)
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

    private void BtnContact_Click(object sender, RoutedEventArgs e)
    {
        OpenContactWindow();
    }

    private void OpenContactWindow()
    {
        var w = new ContactWindow { Owner = this };
        w.ShowDialog();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}