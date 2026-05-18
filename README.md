# SyncSpace Desktop Native

C# / WPF native desktop client for [SyncSpace](https://github.com/tmdry4530/SyncSpace).

## What changed from v0.1.0

v0.1.0 was a WebView2 wrapper. v0.2.0 removes WebView2 and renders the main app screens with native WPF controls.

Native screens/features:

- Login screen via Supabase Auth REST API
- Workspace list
- Workspace create / join through existing backend API
- Channel list and create
- Document list and create
- Native chat panel using Supabase REST insert/select
- Native document editor using WPF RichTextBox
- Slash-command-style toolbar buttons: H1, checklist, quote, code
- Knowledge rail extracting headings, `[[links]]`, and `#tags`
- Realtime connection probe against the existing SyncSpace WebSocket route, with polling fallback status

## Important limitation

The original web app uses Tiptap + Yjs CRDT for true collaborative rich-text document state. This native client has a native editor and realtime probe, but does **not** implement full Yjs CRDT binary sync in C#. Document body snapshots are local to the running app in this version. Chat/workspace/channel/document metadata use Supabase/backend APIs.

## Configure

Edit `SyncSpaceDesktop/appsettings.json` or the published `appsettings.json` next to `SyncSpace.exe`:

```json
{
  "SupabaseUrl": "https://YOUR_PROJECT.supabase.co",
  "SupabaseAnonKey": "YOUR_SUPABASE_ANON_KEY",
  "BackendUrl": "http://localhost:1234",
  "WebSocketUrl": "ws://localhost:1234",
  "WindowTitle": "SyncSpace Native"
}
```

Do not put service-role keys in this app.

## Build

```powershell
dotnet restore SyncSpaceDesktop/SyncSpaceDesktop.csproj
dotnet publish SyncSpaceDesktop/SyncSpaceDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -o artifacts/win-x64-native
```

## Run

Unzip the release, edit `appsettings.json`, then run:

```powershell
.\SyncSpace.exe
```
