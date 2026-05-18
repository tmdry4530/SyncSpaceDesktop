# SyncSpace Desktop

C# / WPF / WebView2 desktop wrapper for [SyncSpace](https://github.com/tmdry4530/SyncSpace).

## What this is

- Windows desktop `.exe` shell written in C#.
- Uses Microsoft Edge WebView2 to run SyncSpace in a native window.
- Default start URL is the deployed SyncSpace web app: `https://sync-space-green.vercel.app/`.
- A built static frontend snapshot is included under `assets/web` as a fallback / packaging reference.

## Requirements

- Windows 10/11
- Microsoft Edge WebView2 Runtime. Most Windows 11 machines already include it.

## Build

```powershell
dotnet restore
dotnet publish SyncSpaceDesktop/SyncSpaceDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -o artifacts/win-x64
```

## Configure start URL

Edit `SyncSpaceDesktop/appsettings.json` before publishing:

```json
{
  "StartUrl": "https://sync-space-green.vercel.app/",
  "UseLocalFallback": true
}
```

If `StartUrl` is empty, the app loads the bundled `assets/web/index.html`.

## Notes

This is intentionally a WebView2 desktop packaging implementation, not a full rewrite of SyncSpace's React/Yjs/Tiptap collaboration engine into native WPF controls.
