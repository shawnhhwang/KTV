# AI-Enhanced KTV 點歌與播放系統 (MVP)

基於 **.NET 8.0 WPF** 與 **Ollama / Gemma 2B LLM** 打造的 AI 增強版 KTV 點歌與播放系統。

本專案採用 **Simplicity First (先求簡單)** 的極簡架構原則，並支持 **Cross-Platform Dev (跨平台相容編譯與測試)**，可在 macOS 上進行建置與執行單元測試，並在 Windows 桌面上部署為完整之 WPF 雙螢幕系統。

---

## 核心功能特色 (Features)
1. **雙螢幕影音播放 (LibVLCSharp)**:
   * 主控制台（主螢幕）與無邊框全螢幕播放視窗（副螢幕/電視）自動定位與分配。
   * 支援 MPG/MP4，點擊即時切換「原/伴唱」聲道複製，無爆音。
2. **低延遲音訊變調管線 (NAudio + SoundTouch)**:
   * 攔截 VLC 浮點 PCM 音訊，透過 C++ SoundTouch 實作即時 $\pm 6$ 半音變調，並使用 NAudio WASAPI 低延遲輸出，確保影音嘴型同步。
3. **LLM 語意解析點歌代理 (Ollama / Gemma 2B)**:
   * 輸入自然語言（如「我想聽周杰倫的歌」或「降 Key 兩格」），系統透過 Ollama 呼叫本地 Gemma 模型，強約束返回 JSON 格式命令，自動執行點歌、Key 調整、播放/暫停或 SQLite 資料庫模糊檢索。
4. **背景 YouTube 下載與 AI 去人聲 separation (yt-dlp + demucs)**:
   * 提供非同步背景單線程佇列，下載網址影片並調用 `demucs` 離線去人聲導出伴奏。
   * WPF 界面即時且非同步刷新下載狀態（進度 0%~100%），下載完成自動將路徑存入 SQLite 並刷新 UI。
5. **Fail Loudly & 健壯性設計**:
   * 所有層次（UI、背景 Task、非同步線程）異常均由全域處理程序攔截寫入 Serilog，確保程式不閃退。
   * 提供完整的 macOS 降級直通模式（若 SoundTouch / VLC / Downloader native 組件不存在，自動以 Mock 機制直通處理，保證跨平台測試不中斷）。

---

## 專案結構 (Project Directory)
```text
KTV/
├── KTV.csproj                # 多目標框架專案檔 (net8.0;net8.0-windows)
├── App.xaml                  # WPF 應用程式定義
├── App.xaml.cs               # 全域初始化、配置載入與異常攔截
├── ControlPanelWindow.xaml   # 主控制台 (UI)
├── ControlPanelWindow.xaml.cs# 控制台邏輯：LLM 意圖執行、SQLite 模糊檢索與背景下載連動
├── PlayerWindow.xaml         # 雙螢幕播放視窗 (UI)
├── PlayerWindow.xaml.cs      # LibVLCSharp 與 NAudio+SoundTouch 音訊變調管線
├── DatabaseBootstrap.cs      # SQLite 資料庫初始化與建表
├── LoggerSetup.cs            # Serilog 日誌引導初始化
├── SoundTouchInterop.cs      # C++ SoundTouch DLL 的 P/Invoke 接口
├── SoundTouchProcessor.cs    # 變調處理包裝與 Bypass 直通邏輯
├── LlmAgentService.cs        # Ollama API 整合與 JSON 強約束解析
├── BackgroundJobService.cs   # 背景任務併發佇列與 yt-dlp + demucs 進程控制
├── appsettings.json          # 系統配置文件
├── DEVELOPMENT.md            # 詳細開發迭代檢查點歷史
├── WIKI.md                   # 系統詳解 Wiki 文檔
└── KTV.Tests/                # 單元測試專案
    ├── KTV.Tests.csproj      # xUnit 測試專案檔 (net8.0)
    ├── DatabaseTests.cs      # 驗證 SQLite & Dapper
    ├── LoggingTests.cs       # 驗證 Serilog
    ├── PlayerTests.cs        # 驗證 VLC 聲道與 SoundTouch 封裝
    └── LlmTests.cs           # 驗證 LLM HTTP 意圖解析 (Mock)
```

---

## 快速開始 (Quick Start)

### 1. 外部環境準備 (Prerequisites)
* **Windows 執行環境**: Windows 11 (建議) / Windows 10。
* **Ollama (本地 LLM API)**:
  * 下載並運行 Ollama (https://ollama.com)。
  * 本地 Pull 模型: `ollama pull gemma2b`。
* **YouTube 下載與分離組件**:
  * 下載 `yt-dlp.exe` 並放置於本機指定目錄。
  * 下載 `demucs` (Python 套件/執行檔) 並配置於本機指定目錄。
* **SoundTouch C++ 庫**:
  * 將 `SoundTouch.dll` 放置於應用程式執行根目錄。

### 2. 設定檔配置 (`appsettings.json`)
```json
{
  "Database": {
    "ConnectionString": "Data Source=KtvDatabase.sqlite;Cache=Shared;"
  },
  "LlmAgent": {
    "Endpoint": "http://localhost:11434/api/generate",
    "ModelName": "gemma2b",
    "TimeoutSeconds": 10
  },
  "ExternalTools": {
    "YtDlpPath": "Tools\\yt-dlp.exe",
    "VocalRemoverPath": "Tools\\demucs.exe",
    "DownloadDirectory": "Library\\Downloads"
  }
}
```

### 3. 編譯與測試 (Build & Test)
本專案支持在 Windows 與 macOS 上進行編譯與測試：

* **還原與編譯**:
  ```bash
  dotnet restore
  dotnet build
  ```
* **執行單元測試**:
  ```bash
  dotnet test KTV.Tests/KTV.Tests.csproj
  ```

---

## 授權條款 (License)
Apache-2.0 License
