using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace SilentLens.Views;

public partial class MapWindow : Window
{
    private readonly double _lat;
    private readonly double _lon;

    public MapWindow(double latitude, double longitude)
    {
        InitializeComponent();
        _lat = latitude;
        _lon = longitude;

        TitleText.Text = $"GPS: {latitude:0.#####}, {longitude:0.#####}";
        Loaded += async (_, _) => await InitMapAsync();
    }

    private async Task InitMapAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SilentLens", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await MapView.EnsureCoreWebView2Async(env);

            MapView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            MapView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            MapView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            MapView.NavigateToString(BuildHtml(_lat, _lon));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Не удалось инициализировать карту:\n\n{ex.Message}",
                "Silent Lens", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string BuildHtml(double lat, double lon)
    {
        var latStr = lat.ToString("0.######", CultureInfo.InvariantCulture);
        var lonStr = lon.ToString("0.######", CultureInfo.InvariantCulture);

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<title>GPS</title>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
<style>
    html, body {{ margin: 0; padding: 0; height: 100%; background: #14141A; }}
    #map {{ width: 100%; height: 100%; }}
</style>
</head>
<body>
<div id=""map""></div>
<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
<script>
    var lat = {latStr};
    var lon = {lonStr};
    var map = L.map('map').setView([lat, lon], 15);
    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
        attribution: '© OpenStreetMap contributors',
        maxZoom: 19
    }}).addTo(map);
    L.marker([lat, lon]).addTo(map)
        .bindPopup('Координаты: ' + lat + ', ' + lon)
        .openPopup();
</script>
</body>
</html>";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) return;
        else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}