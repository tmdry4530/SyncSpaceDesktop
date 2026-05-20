# SyncSpace Desktop Native

SyncSpace Desktop Native는 [SyncSpace](https://github.com/tmdry4530/SyncSpace)를 Windows 데스크톱 환경에서 사용할 수 있도록 만든 C# / WPF 네이티브 클라이언트입니다.

기존 웹앱을 단순히 WebView로 감싼 버전이 아니라, 워크스페이스·채널·문서·채팅 화면을 WPF 네이티브 UI로 다시 구성한 데스크톱 앱입니다.

## 프로젝트 목표

SyncSpace는 팀이 문서와 채팅을 한 화면에서 다루는 협업 공간입니다. 이 데스크톱 클라이언트는 브라우저 없이도 Windows PC에서 SyncSpace를 실행하고, 로컬 또는 원격 SyncSpace backend / Supabase 프로젝트에 접속하는 것을 목표로 합니다.

주요 목적:

- Windows `.exe`로 바로 실행 가능한 SyncSpace 클라이언트 제공
- React/WebView 의존도를 줄인 네이티브 WPF UI 제공
- 기존 SyncSpace backend, Supabase Auth, Supabase REST API 재사용
- 같은 LAN 안에서 한 PC가 서버를 열고 다른 PC들이 접속하는 운영 방식 지원

## 주요 기능

- Supabase Auth 기반 로그인
- 워크스페이스 목록 조회
- 워크스페이스 생성 / 초대코드 참가
- 채널 목록 조회 / 채널 생성
- 문서 목록 조회 / 문서 생성
- WPF `RichTextBox` 기반 네이티브 문서 편집 화면
- H1, 체크리스트, 인용, 코드 삽입 버튼
- 채널별 채팅 패널
- 문서 내용에서 `[[링크]]`, `#태그`, 제목을 추출하는 Knowledge Rail
- SyncSpace WebSocket endpoint 연결 상태 확인
- WebSocket 연결 실패 시 polling mode 상태 표시
- 어두운 네이티브 데스크톱 테마

## 현재 버전

최신 릴리즈는 `v0.2.3`입니다.

- Release: https://github.com/tmdry4530/SyncSpaceDesktop/releases/tag/v0.2.3
- Windows x64 다운로드: https://github.com/tmdry4530/SyncSpaceDesktop/releases/download/v0.2.3/SyncSpaceDesktop-win-x64-native-v0.2.3.zip

## 버전 히스토리

### v0.1.0

초기 Windows `.exe` 배포 버전입니다. 기존 SyncSpace 웹 UI를 WPF + WebView2로 실행하는 wrapper 방식이었습니다.

### v0.2.0

WebView2 wrapper를 제거하고 메인 화면을 C# / WPF 네이티브 UI로 교체했습니다.

### v0.2.1

네이티브 다크 테마를 개선했습니다. 앱 배경, 패널, 에디터, 리스트 선택 색상, 버튼, realtime 상태 표시 색상을 조정했습니다.

### v0.2.2

비활성화된 회색 버튼의 글자가 흰색으로 보이던 대비 문제를 수정했습니다.

### v0.2.3

기본 `appsettings.json`을 Supabase production 프로젝트와 Railway backend/WebSocket endpoint로 맞춘 배포 버전입니다.

## 기술 스택

- C#
- .NET 8
- WPF
- Supabase Auth REST API
- Supabase PostgREST
- HTTP backend API
- WebSocket connection probe

## 앱 구조

```txt
SyncSpaceDesktop/
├── SyncSpaceDesktop/
│   ├── App.xaml              # 전역 테마 / WPF resource
│   ├── App.xaml.cs
│   ├── MainWindow.xaml       # 메인 네이티브 UI
│   ├── MainWindow.xaml.cs    # 로그인, API 호출, UI 이벤트, editor/chat logic
│   ├── appsettings.json      # 실행 설정
│   └── SyncSpaceDesktop.csproj
├── artifacts/                # publish output / release zip
└── README.md
```

## 설정

릴리즈 zip을 압축 해제한 뒤 `SyncSpace.exe` 옆의 `appsettings.json`을 수정합니다.

```json
{
  "SupabaseUrl": "https://YOUR_PROJECT.supabase.co",
  "SupabaseAnonKey": "YOUR_SUPABASE_ANON_KEY",
  "BackendUrl": "http://localhost:1234",
  "WebSocketUrl": "ws://localhost:1234",
  "WindowTitle": "SyncSpace Native"
}
```

주의:

- 데스크톱 앱에는 Supabase `anon key`만 넣어야 합니다.
- Supabase `service role key`는 절대 넣지 마세요.
- 다른 PC의 서버에 접속할 때 `localhost`를 쓰면 안 됩니다. 서버 PC의 LAN IP를 사용해야 합니다.

예시:

```json
{
  "SupabaseUrl": "https://YOUR_PROJECT.supabase.co",
  "SupabaseAnonKey": "YOUR_SUPABASE_ANON_KEY",
  "BackendUrl": "http://192.168.0.25:1234",
  "WebSocketUrl": "ws://192.168.0.25:1234",
  "WindowTitle": "SyncSpace Native"
}
```

## Windows에서 실행

1. 릴리즈 zip 다운로드
2. 압축 해제
3. `appsettings.json` 수정
4. `SyncSpace.exe` 실행

```powershell
.\SyncSpace.exe
```

## LAN에서 서버 열고 접속하기

서버를 여는 Windows PC에서 SyncSpace backend를 `0.0.0.0`으로 실행합니다.

```powershell
$env:HOST="0.0.0.0"
$env:PORT="1234"
pnpm --filter server start
```

서버 PC의 IPv4 주소 확인:

```powershell
ipconfig
```

다른 Windows PC에서는 `appsettings.json`에 서버 PC IP를 넣습니다.

```json
{
  "BackendUrl": "http://SERVER_PC_IP:1234",
  "WebSocketUrl": "ws://SERVER_PC_IP:1234"
}
```

방화벽에서 TCP `1234` 포트를 허용해야 합니다.

```powershell
New-NetFirewallRule `
  -DisplayName "SyncSpace Backend 1234" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 1234 `
  -Action Allow
```

접속 테스트:

```txt
http://SERVER_PC_IP:1234/health
```

## 빌드

macOS 또는 Windows에서 Windows x64 self-contained build를 만들 수 있습니다.

```powershell
dotnet restore SyncSpaceDesktop/SyncSpaceDesktop.csproj
dotnet build SyncSpaceDesktop/SyncSpaceDesktop.csproj -c Release -r win-x64 --self-contained true
dotnet publish SyncSpaceDesktop/SyncSpaceDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -o artifacts/win-x64-native
```

## 한계

원본 SyncSpace 웹앱은 Tiptap + Yjs CRDT를 사용해 진짜 공동 편집 rich-text 상태를 동기화합니다.

현재 네이티브 클라이언트는 WPF 네이티브 에디터와 WebSocket 연결 상태 확인은 제공하지만, C#에서 Yjs CRDT binary sync를 완전히 구현하지는 않았습니다.

현재 제한사항:

- 문서 본문 공동 편집 CRDT 미구현
- Tiptap 수준의 rich text schema 완전 호환 미구현
- cursor / presence / awareness 동기화 미구현
- 문서 본문 snapshot은 현재 앱 실행 환경 중심으로 동작

채팅, 워크스페이스, 채널, 문서 메타데이터는 기존 Supabase/backend API를 사용합니다.

## 보안 메모

이 앱은 사용자의 PC에 배포되는 데스크톱 클라이언트입니다. 따라서 앱 설정이나 바이너리 안에 서버 전용 credential을 넣으면 안 됩니다.

절대 넣으면 안 되는 값:

- Supabase service role key
- DB password
- backend admin token
- private API key

클라이언트에는 공개 가능한 Supabase anon key만 사용하세요.

## 라이선스

이 저장소는 SyncSpace Desktop Native 클라이언트 구현을 담고 있습니다. 원본 SyncSpace 프로젝트는 https://github.com/tmdry4530/SyncSpace 를 참고하세요.
