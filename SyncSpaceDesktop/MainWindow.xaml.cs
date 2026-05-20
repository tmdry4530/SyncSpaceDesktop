using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SyncSpaceDesktop;

public partial class MainWindow : Window
{
    private readonly DesktopConfig _config = DesktopConfig.Load();
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private Session? _session;
    private readonly ObservableCollection<Workspace> _workspaces = new();
    private readonly ObservableCollection<Channel> _channels = new();
    private readonly ObservableCollection<DocumentMeta> _documents = new();
    private readonly ObservableCollection<MessageItem> _messages = new();
    private readonly ObservableCollection<string> _insights = new();
    private readonly Dictionary<string, string> _localDocumentBodies = new();
    private ClientWebSocket? _presenceSocket;
    private Workspace? SelectedWorkspace => WorkspaceList.SelectedItem as Workspace;
    private Channel? SelectedChannel => ChannelList.SelectedItem as Channel;
    private DocumentMeta? SelectedDocument => DocumentList.SelectedItem as DocumentMeta;

    public MainWindow()
    {
        InitializeComponent();
        Title = _config.WindowTitle;
        WorkspaceList.ItemsSource = _workspaces;
        ChannelList.ItemsSource = _channels;
        DocumentList.ItemsSource = _documents;
        MessageList.ItemsSource = _messages;
        InsightList.ItemsSource = _insights;
        Status("native shell ready");
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        LoginError.Text = "";
        if (!_config.HasSupabase)
        {
            LoginError.Text = "설정(appsettings.json)에 SupabaseUrl / SupabaseAnonKey를 먼저 넣어야 함.";
            return;
        }
        try
        {
            Status("logging in...");
            var payload = new { email = EmailBox.Text.Trim(), password = PasswordBox.Password };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.SupabaseUrl}/auth/v1/token?grant_type=password");
            request.Headers.Add("apikey", _config.SupabaseAnonKey);
            request.Content = JsonContent.Create(payload);
            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(body);
            _session = JsonSerializer.Deserialize<Session>(body, _jsonOptions) ?? throw new InvalidOperationException("invalid session");
            LoginPanel.Visibility = Visibility.Collapsed;
            AppPanel.Visibility = Visibility.Visible;
            await RefreshAllAsync();
            Status("logged in");
        }
        catch (Exception ex)
        {
            LoginError.Text = ex.Message;
            Status("login failed");
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show($"설정 파일:\\n{DesktopConfig.Path}\\n\\nSupabaseUrl: {Mask(_config.SupabaseUrl)}\\nBackendUrl: {_config.BackendUrl}\\nWebSocketUrl: {_config.WebSocketUrl}", "SyncSpace Native 설정");
    }

    private async Task RefreshAllAsync()
    {
        if (_session is null) return;
        await LoadWorkspacesAsync();
        if (_workspaces.Count > 0 && WorkspaceList.SelectedItem is null) WorkspaceList.SelectedIndex = 0;
        await StartPresenceProbeAsync();
    }

    private async void WorkspaceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedWorkspace is null) return;
        await LoadChannelsAsync(SelectedWorkspace.Id);
        await LoadDocumentsAsync(SelectedWorkspace.Id);
        if (_channels.Count > 0) ChannelList.SelectedIndex = 0;
        if (_documents.Count > 0) DocumentList.SelectedIndex = 0;
    }

    private async void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedChannel is null) return;
        ChannelTitle.Text = $"#{SelectedChannel.Name}";
        await LoadMessagesAsync(SelectedChannel.Id);
        await StartPresenceProbeAsync();
    }

    private void DocumentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedDocument is null) return;
        DocumentTitle.Text = SelectedDocument.Title;
        DocMetaText.Text = $"updated {SelectedDocument.UpdatedAt}";
        SetEditorText(_localDocumentBodies.TryGetValue(SelectedDocument.Id, out var text) ? text : $"# {SelectedDocument.Title}\n\n이 문서는 네이티브 C# 에디터에서 편집 중.\n[[관련문서]] #syncspace\n");
        UpdateInsights();
    }

    private async void CreateWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var name = NewWorkspaceBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        await BackendPostAsync("/api/workspaces", new { name });
        NewWorkspaceBox.Text = "";
        await LoadWorkspacesAsync();
    }

    private async void JoinWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var inviteCode = InviteCodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(inviteCode)) return;
        await BackendPostAsync("/api/workspaces/join", new { inviteCode });
        InviteCodeBox.Text = "";
        await LoadWorkspacesAsync();
    }

    private async void CreateChannel_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedWorkspace is null || _session is null) return;
        var name = NewChannelBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        await SupabaseInsertAsync("channels", new { workspace_id = SelectedWorkspace.Id, name, created_by = _session.User.Id });
        NewChannelBox.Text = "";
        await LoadChannelsAsync(SelectedWorkspace.Id);
    }

    private async void CreateDocument_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedWorkspace is null || _session is null) return;
        var title = NewDocumentBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title)) return;
        await SupabaseInsertAsync("documents", new { workspace_id = SelectedWorkspace.Id, title, created_by = _session.User.Id });
        NewDocumentBox.Text = "";
        await LoadDocumentsAsync(SelectedWorkspace.Id);
    }

    private async void SendMessage_Click(object sender, RoutedEventArgs e) => await SendCurrentMessageAsync();

    private async void MessageBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            await SendCurrentMessageAsync();
            e.Handled = true;
        }
    }

    private async Task SendCurrentMessageAsync()
    {
        if (SelectedChannel is null || _session is null) return;
        var content = MessageBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(content)) return;
        await SupabaseInsertAsync("messages", new { channel_id = SelectedChannel.Id, user_id = _session.User.Id, content, client_id = Guid.NewGuid().ToString("N") });
        MessageBox.Text = "";
        await LoadMessagesAsync(SelectedChannel.Id);
    }

    private void InsertH1_Click(object sender, RoutedEventArgs e) => InsertEditorText("# 제목\n");
    private void InsertTodo_Click(object sender, RoutedEventArgs e) => InsertEditorText("- [ ] 할 일\n");
    private void InsertQuote_Click(object sender, RoutedEventArgs e) => InsertEditorText("> 인용\n");
    private void InsertCode_Click(object sender, RoutedEventArgs e) => InsertEditorText("```\ncode\n```\n");

    private void SaveDocument_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDocument is null) return;
        _localDocumentBodies[SelectedDocument.Id] = GetEditorText();
        EditorStatus.Text = "saved locally (native editor snapshot)";
        UpdateInsights();
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        EditorStatus.Text = "editing";
        UpdateInsights();
    }

    private async Task LoadWorkspacesAsync()
    {
        var rows = await SupabaseSelectAsync<List<WorkspaceRow>>("workspaces", "id,name,owner_id,invite_code,created_at", "order=created_at.asc");
        _workspaces.ReplaceWith(rows.Select(x => new Workspace(x.Id, x.Name, x.OwnerId, x.InviteCode, x.CreatedAt)));
        Status($"workspaces: {_workspaces.Count}");
    }

    private async Task LoadChannelsAsync(string workspaceId)
    {
        var rows = await SupabaseSelectAsync<List<ChannelRow>>("channels", "id,workspace_id,name,created_by,created_at", $"workspace_id=eq.{workspaceId}&order=created_at.asc");
        _channels.ReplaceWith(rows.Select(x => new Channel(x.Id, x.WorkspaceId, x.Name, x.CreatedBy, x.CreatedAt)));
    }

    private async Task LoadDocumentsAsync(string workspaceId)
    {
        var rows = await SupabaseSelectAsync<List<DocumentRow>>("documents", "id,workspace_id,title,created_by,updated_at", $"workspace_id=eq.{workspaceId}&order=updated_at.desc");
        _documents.ReplaceWith(rows.Select(x => new DocumentMeta(x.Id, x.WorkspaceId, x.Title, x.CreatedBy, x.UpdatedAt)));
    }

    private async Task LoadMessagesAsync(string channelId)
    {
        var rows = await SupabaseSelectAsync<List<MessageRow>>("messages", "id,channel_id,user_id,content,client_id,created_at", $"channel_id=eq.{channelId}&order=created_at.desc&limit=50");
        _messages.ReplaceWith(rows.OrderBy(x => x.CreatedAt).Select(x => new MessageItem($"{ShortId(x.UserId)} · {x.CreatedAt:g}\n{x.Content}")));
    }

    private async Task<T> SupabaseSelectAsync<T>(string table, string select, string query)
    {
        EnsureSession();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.SupabaseUrl}/rest/v1/{table}?select={Uri.EscapeDataString(select)}&{query}");
        AddSupabaseHeaders(request);
        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(body);
        return JsonSerializer.Deserialize<T>(body, _jsonOptions) ?? throw new InvalidOperationException("invalid response");
    }

    private async Task SupabaseInsertAsync(string table, object payload)
    {
        EnsureSession();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.SupabaseUrl}/rest/v1/{table}");
        AddSupabaseHeaders(request);
        request.Headers.Add("Prefer", "return=minimal");
        request.Content = JsonContent.Create(payload, options: _jsonOptions);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
    }

    private async Task BackendPostAsync(string path, object payload)
    {
        EnsureSession();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BackendUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session!.AccessToken);
        request.Content = JsonContent.Create(payload, options: _jsonOptions);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
    }

    private async Task StartPresenceProbeAsync()
    {
        if (SelectedWorkspace is null || SelectedChannel is null) return;
        try
        {
            _presenceSocket?.Dispose();
            _presenceSocket = new ClientWebSocket();
            if (_session is not null) _presenceSocket.Options.SetRequestHeader("Authorization", $"Bearer {_session.AccessToken}");
            var uri = new Uri($"{_config.WebSocketUrl.TrimEnd('/')}/chat/{SelectedWorkspace.Id}/{SelectedChannel.Id}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await _presenceSocket.ConnectAsync(uri, cts.Token);
            PresenceText.Text = "realtime connected";
            PresenceText.Foreground = Brushes.LightGreen;
        }
        catch
        {
            PresenceText.Text = "polling mode";
            PresenceText.Foreground = Brushes.Khaki;
        }
    }

    private void AddSupabaseHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("apikey", _config.SupabaseAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session!.AccessToken);
    }

    private void EnsureSession()
    {
        if (_session is null) throw new InvalidOperationException("로그인이 필요함");
    }

    private void InsertEditorText(string text) => EditorBox.CaretPosition.InsertTextInRun(text);
    private string GetEditorText() => new TextRange(EditorBox.Document.ContentStart, EditorBox.Document.ContentEnd).Text;
    private void SetEditorText(string text)
    {
        EditorBox.Document.Blocks.Clear();
        EditorBox.Document.Blocks.Add(new Paragraph(new Run(text)));
    }

    private void UpdateInsights()
    {
        if (InsightList is null) return;
        var text = GetEditorText();
        var links = Regex.Matches(text, @"\[\[([^\]]+)\]\]").Select(m => $"link: {m.Groups[1].Value}");
        var tags = Regex.Matches(text, @"(?<!\w)#([\p{L}\p{N}_-]+)").Select(m => $"tag: #{m.Groups[1].Value}");
        var headings = Regex.Matches(text, @"(?m)^#\s+(.+)$").Select(m => $"heading: {m.Groups[1].Value.Trim()}");
        _insights.ReplaceWith(headings.Concat(links).Concat(tags).Distinct().Take(30));
    }

    private void Status(string text) => StatusText.Text = text;
    private static string Mask(string value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Length <= 12 ? "***" : value[..8] + "…";
    private static string ShortId(string value) => string.IsNullOrWhiteSpace(value) ? "user" : value[..Math.Min(8, value.Length)];
}

public static class ObservableExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }
}

public sealed record DesktopConfig(string SupabaseUrl, string SupabaseAnonKey, string BackendUrl, string WebSocketUrl, string WindowTitle)
{
    public bool HasSupabase => !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseAnonKey);
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    public static DesktopConfig Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                return JsonSerializer.Deserialize<DesktopConfig>(File.ReadAllText(Path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? Default;
            }
        }
        catch { }
        return Default;
    }
    public static DesktopConfig Default => new("", "", "http://localhost:1234", "ws://localhost:1234", "SyncSpace Native");
}

public sealed record Workspace(string Id, string Name, string OwnerId, string InviteCode, DateTimeOffset CreatedAt);
public sealed record Channel(string Id, string WorkspaceId, string Name, string CreatedBy, DateTimeOffset CreatedAt);
public sealed record DocumentMeta(string Id, string WorkspaceId, string Title, string CreatedBy, DateTimeOffset UpdatedAt);
public sealed record MessageItem(string Text)
{
    public override string ToString() => Text;
}

public sealed record Session(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("user")] SupabaseUser User);
public sealed record SupabaseUser([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("email")] string? Email);

public sealed record WorkspaceRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner_id")] string OwnerId,
    [property: JsonPropertyName("invite_code")] string InviteCode,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
public sealed record ChannelRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("created_by")] string CreatedBy,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
public sealed record DocumentRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_by")] string CreatedBy,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
public sealed record MessageRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("channel_id")] string ChannelId,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
