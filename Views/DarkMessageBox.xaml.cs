using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SilentLens.Views;

public enum DarkMessageIcon
{
    Info,
    Warning,
    Error,
    Question
}

public enum DarkMessageButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel
}

public enum DarkMessageResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No
}

public partial class DarkMessageBox : Window
{
    private DarkMessageResult _result = DarkMessageResult.None;

    private DarkMessageBox()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _result = DarkMessageResult.Cancel;
        Close();
    }

    public static DarkMessageResult Show(
        Window? owner,
        string message,
        string title = "Silent Lens",
        DarkMessageIcon icon = DarkMessageIcon.Info,
        DarkMessageButtons buttons = DarkMessageButtons.Ok)
    {
        var box = new DarkMessageBox
        {
            TitleText = { Text = title },
            MessageText = { Text = message }
        };

        (box.IconText.Text, box.IconText.Foreground) = icon switch
        {
            DarkMessageIcon.Info =>
                ("ℹ", (Brush)Application.Current.FindResource("BrushAccent")),
            DarkMessageIcon.Warning =>
                ("⚠", new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41))),
            DarkMessageIcon.Error =>
                ("✖", (Brush)Application.Current.FindResource("BrushDanger")),
            DarkMessageIcon.Question =>
                ("?", (Brush)Application.Current.FindResource("BrushAccent")),
            _ => ("ℹ", (Brush)Application.Current.FindResource("BrushAccent"))
        };

        box.BuildButtons(buttons);

        if (owner != null)
        {
            box.Owner = owner;
            box.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        box.ShowDialog();
        return box._result;
    }

    private void BuildButtons(DarkMessageButtons buttons)
    {
        ButtonsPanel.Children.Clear();

        switch (buttons)
        {
            case DarkMessageButtons.Ok:
                AddButton("OK", DarkMessageResult.Ok, isAccent: true);
                break;

            case DarkMessageButtons.OkCancel:
                AddButton("Отмена", DarkMessageResult.Cancel);
                AddButton("OK", DarkMessageResult.Ok, isAccent: true);
                break;

            case DarkMessageButtons.YesNo:
                AddButton("Нет", DarkMessageResult.No);
                AddButton("Да", DarkMessageResult.Yes, isAccent: true);
                break;

            case DarkMessageButtons.YesNoCancel:
                AddButton("Отмена", DarkMessageResult.Cancel);
                AddButton("Нет", DarkMessageResult.No);
                AddButton("Да", DarkMessageResult.Yes, isAccent: true);
                break;
        }
    }

    private void AddButton(string text, DarkMessageResult result, bool isAccent = false)
    {
        var btn = new Button
        {
            Content = text,
            MinWidth = 100,
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)Application.Current.FindResource(
                isAccent ? "AccentButton" : "BaseButton")
        };
        btn.Click += (_, _) =>
        {
            _result = result;
            Close();
        };
        ButtonsPanel.Children.Add(btn);
    }
}