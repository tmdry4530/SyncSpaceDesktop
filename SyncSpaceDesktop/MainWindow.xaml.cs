using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SyncSpaceDesktop;

public partial class MainWindow : Window
{
    private readonly DesktopConfig _config;
    private Uri? _homeUri;

    public MainWindow()
    {
        InitializeComponent();
        _config = DesktopConfig.Load();
        Title = _config.WindowTitle;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.DocumentTitleChanged += (_, _) =>
            {
                var title = Browser.CoreWebView2.DocumentTitle;
                Title = string.IsNullOrWhiteSpace(title) ? _config.WindowTitle : $"{title} - SyncSpace Desktop";
            };
            Browser.CoreWebView2.NavigationCompleted += (_, _) => UpdateNavigationButtons();

            _homeUri = ResolveHomeUri();
            Browser.Source = _homeUri;
            UpdateNavigationButtons();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "Microsoft Edge WebView2 Runtime is required. Install it from Microsoft, then run SyncSpace again.",
                "SyncSpace Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SyncSpace Desktop startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private Uri ResolveHomeUri()
    {
        if (!string.IsNullOrWhiteSpace(_config.StartUrl) && Uri.TryCreate(_config.StartUrl, UriKind.Absolute, out var remoteUri))
        {
            return remoteUri;
        }

        var localIndex = Path.Combine(AppContext.BaseDirectory, "assets", "web", "index.html");
        if (_config.UseLocalFallback && File.Exists(localIndex))
        {
            return new Uri(localIndex);
        }

        throw new InvalidOperationException("No valid StartUrl configured and bundled assets/web/index.html was not found.");
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack) Browser.GoBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoForward) Browser.GoForward();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        Browser.Reload();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_homeUri is not null) Browser.Source = _homeUri;
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = Browser.CanGoBack;
        ForwardButton.IsEnabled = Browser.CanGoForward;
    }
}

public sealed record DesktopConfig(string StartUrl, bool UseLocalFallback, string WindowTitle)
{
    public static DesktopConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return Default;

        try
        {
            var config = JsonSerializer.Deserialize<DesktopConfig>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config ?? Default;
        }
        catch
        {
            return Default;
        }
    }

    public static DesktopConfig Default => new(
        "https://sync-space-green.vercel.app/",
        true,
        "SyncSpace Desktop");
}
