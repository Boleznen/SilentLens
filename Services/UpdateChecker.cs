using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SilentLens.Services;

public sealed class UpdateInfo
{
    public string TagName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}

public static class UpdateChecker
{
    // ВАЖНО: замени на свой репозиторий после публикации
    private const string GitHubRepo = "Boleznen/SilentLens";

    private const string CurrentVersion = "1.0.0";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static UpdateChecker()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SilentLens/" + CurrentVersion);
    }

    /// <summary>
    /// Проверить последний релиз на GitHub. Возвращает null, если обновлений нет
    /// или не удалось подключиться.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString() ?? "";
            var body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
            var publishedAt = root.TryGetProperty("published_at", out var pubEl)
                ? DateTime.Parse(pubEl.GetString() ?? DateTime.UtcNow.ToString())
                : DateTime.UtcNow;

            var downloadUrl = "";
            if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                var firstAsset = assets[0];
                if (firstAsset.TryGetProperty("browser_download_url", out var dlEl))
                    downloadUrl = dlEl.GetString() ?? "";
            }

            var version = tagName.TrimStart('v', 'V');

            if (IsNewer(version, CurrentVersion))
            {
                return new UpdateInfo
                {
                    TagName = tagName,
                    Version = version,
                    DownloadUrl = downloadUrl,
                    HtmlUrl = htmlUrl,
                    Body = body,
                    PublishedAt = publishedAt
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNewer(string remote, string local)
    {
        try
        {
            var r = new Version(remote);
            var l = new Version(local);
            return r > l;
        }
        catch
        {
            return false;
        }
    }

    public static string GetCurrentVersion() => CurrentVersion;
}