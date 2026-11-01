using System;
using System.IO;
using System.Text.Json;

namespace AlmasCashierDesktop;

internal sealed class CashierConfig
{
    public string ServerUrl { get; set; } = "https://sejadalmas.iq";
    public static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlmasCashierDesktop");
    public static string FileName => Path.Combine(Home, "settings.json");
    public static string WebViewFolder => Path.Combine(Home, "WebView2");

    public static CashierConfig Load()
    {
        try
        {
            if (!File.Exists(FileName)) return new CashierConfig();
            var loaded = JsonSerializer.Deserialize<CashierConfig>(File.ReadAllText(FileName));
            return loaded is not null && IsValidServer(loaded.ServerUrl) ? loaded : new CashierConfig();
        }
        catch { return new CashierConfig(); }
    }

    public void Save()
    {
        if (!IsValidServer(ServerUrl)) throw new ArgumentException("آدرس سرور باید HTTP یا HTTPS معتبر باشد.");
        Directory.CreateDirectory(Home);
        File.WriteAllText(FileName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool IsValidServer(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && uri.UserInfo.Length == 0
        && (uri.AbsolutePath == "/" || uri.AbsolutePath.Length == 0)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
