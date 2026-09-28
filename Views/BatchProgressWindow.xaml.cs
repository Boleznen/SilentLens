using System;
using System.Windows;
using System.Windows.Input;
using SilentLens.Services;

namespace SilentLens.Views;

public partial class BatchProgressWindow : Window
{
    public event EventHandler? CancelRequested;

    public BatchProgressWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(BatchProgressInfo info)
    {
        CounterText.Text = $"Обработано {info.Current} из {info.Total}";
        CurrentFileText.Text = info.CurrentFile;

        if (info.Total > 0)
            Progress.Value = (double)info.Current / info.Total * 100;
    }

    public void SetFinished()
    {
        BtnCancel.Content = "Закрыть";
        BtnCancel.Click -= BtnCancel_Click;
        BtnCancel.Click += (_, _) => Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
        BtnCancel.IsEnabled = false;
        BtnCancel.Content = "Отмена...";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}