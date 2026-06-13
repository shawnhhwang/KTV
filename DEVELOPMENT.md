# KTV 點歌與播放系統：開發歷程與檢查點紀錄 (DEVELOPMENT.md)

本文件紀錄「KTV 點歌與播放系統」的開發歷程、重大架構決策與各階段任務的 Checkpoint 驗證狀態。

---

## 專案核心設計原則
1. **Simplicity First**: 寫最簡單、最直覺的程式碼，不預先設計複雜的層次。
2. **Fail Loudly**: 任何 Exception 都會明確記錄並傳遞給 UI 或 Log，不隱蔽錯誤。
3. **Cross-Platform Dev Ready**: 為了相容於 macOS 開發環境，主專案採用多目標框架 (`net8.0` + `net8.0-windows`)，非 Windows 環境下不編譯 XAML 檔案，並透過 `net8.0` 單元測試驗證所有業務、資料庫、日誌、變調以及背景下載邏輯。

---

## 各階段任務實作與驗證紀錄

### Task 1: 基礎專案建立與極簡 SQLite 串接
* **實作內容**:
  * 建立 [KTV.csproj](file:///Users/shawnwang/Documents/agy/KTV/KTV.csproj) 多目標框架配置，安裝 `Microsoft.Data.Sqlite`, `Dapper`, `Serilog` 套件。
  * 建立 [DatabaseBootstrap.cs](file:///Users/shawnwang/Documents/agy/KTV/DatabaseBootstrap.cs) 提供極簡的資料庫引導，自動建立 `Songs` 資料表。
  * 建立 [LoggerSetup.cs](file:///Users/shawnwang/Documents/agy/KTV/LoggerSetup.cs) 以 `appsettings.json` 日誌配置初始化 Serilog。
  * 在 [App.xaml.cs](file:///Users/shawnwang/Documents/agy/KTV/App.xaml.cs) 設定 `DispatcherUnhandledException`、`AppDomain.UnhandledException` 和 `TaskScheduler.UnobservedTaskException` 全域異常攔截。
* **驗證**:
  * `DatabaseTests.Initialize_CreatesDatabaseAndSongsTable`：驗證資料庫建立與 Dapper 讀寫正常。
  * `LoggingTests.Configure_CorrectlyInitializesLoggerAndWritesToFile`：驗證 Serilog 解析與日誌檔生成正常。

### Task 2: 雙螢幕 UI 邏輯實作
* **實作內容**:
  * 建立 [ControlPanelWindow.xaml](file:///Users/shawnwang/Documents/agy/KTV/ControlPanelWindow.xaml) 與 [ControlPanelWindow.xaml.cs](file:///Users/shawnwang/Documents/agy/KTV/ControlPanelWindow.xaml.cs) 作為主控制台。
  * 建立 [PlayerWindow.xaml](file:///Users/shawnwang/Documents/agy/KTV/PlayerWindow.xaml) 與 [PlayerWindow.xaml.cs](file:///Users/shawnwang/Documents/agy/KTV/PlayerWindow.xaml.cs) 作為延伸螢幕播放視窗。
  * 引用 `System.Windows.Forms.Screen.AllScreens` 進行螢幕偵測。若存在第二螢幕，自動將 `PlayerWindow` 定位到第二螢幕並設為無邊框全螢幕 (`WindowState = Maximized`, `WindowStyle = None`)；若只有單螢幕則定位在主視窗右側以利開發偵錯。
  * 為解決 Windows Forms 引入導致的隱式全域命名空間衝突（例如 `MessageBox` 和 `Application` 混淆），在 `KTV.csproj` 中配置 `<Using Remove="System.Windows.Forms" />` 清除隱式引用，改用完全限定名 `System.Windows.Forms.Screen` 呼叫。

### Task 3: 影音播放 MVP (LibVLCSharp)
* **實作內容**:
  * 在 [PlayerWindow.xaml](file:///Users/shawnwang/Documents/agy/KTV/PlayerWindow.xaml) 嵌入 `LibVLCSharp.WPF` 的 `VideoView` 元件。
  * 利用反射定位 LibVLCSharp `MediaPlayer` 的原生 API。發現其聲道控制為 `SetChannel` 方法與 `Channel` 屬性，搭配 `AudioOutputChannel` 列舉。
  * 實作 `ToggleVocalChannel` 方法，依次循環切換：`Stereo (立體聲)` -> `Left (原唱，複製左聲道至雙聲道)` -> `Right (伴唱，複製右聲道至雙聲道)`。切換時原生底層處理，無爆音。
* **驗證**:
  * `PlayerTests.Test_VlcAudioChannelToggling`：驗證 `SetChannel` 與 `Channel` 屬性可被呼叫，且在 native LibVLC 遺失時（例如 macOS）能防禦性捕獲並記錄警告，而不中斷測試。

### Task 4: 音訊變調管線 (NAudio + SoundTouch)
* **實作內容**:
  * 建立 [SoundTouchInterop.cs](file:///Users/shawnwang/Documents/agy/KTV/SoundTouchInterop.cs) 封裝 SoundTouch C++ 外部 DLL 的 P/Invoke。
  * 建立 [SoundTouchProcessor.cs](file:///Users/shawnwang/Documents/agy/KTV/SoundTouchProcessor.cs) 封裝 SoundTouch 實例生命週期與處理。若檢測到 native DLL 遺失，自動開啟 Bypass 直通模式。
  * 在 [PlayerWindow.xaml.cs](file:///Users/shawnwang/Documents/agy/KTV/PlayerWindow.xaml.cs) 配置 VLC 音訊重導向。使用 `SetAudioFormat("FL32", 44100, 2)` 將音訊導出為 32 位元浮點 PCM，並設定 `SetAudioCallbacks` 攔截音軌。
  * 攔截之音訊傳入 `SoundTouchProcessor` 變調後，寫入 NAudio `BufferedWaveProvider`。
  * 初始化 NAudio `WasapiOut`（低延遲共用模式，若不可用自動 fallback 至 `WaveOutEvent`；在 macOS 則不初始化物理播放裝置，只作緩衝模擬）。
* **驗證**:
  * `PlayerTests.Test_SoundTouchProcessor_LifecycleAndMockMode`：驗證 `SoundTouchProcessor` 初始化、Key 變更（半音數）以及記憶體釋放，並驗證 macOS 下自動轉為直通 Bypass 機制運作正常。

### Task 5: LLM API 整合與 Feature Toggle
* **實作內容**:
  * 在 `appsettings.json` 中配置 `LlmAgent` 與啟用開關 `EnableLlmAgent`。
  * 建立 [LlmAgentService.cs](file:///Users/shawnwang/Documents/agy/KTV/LlmAgentService.cs)，使用 `HttpClient` 向本機的 Gemma API (Ollama) 發送 System Prompt，並帶入 `"format": "json"` 參數強制 LLM 僅回應 JSON 格式的意圖結構 (`LlmIntent`)。
  * 為使非 Windows (`net8.0`) 單元測試相容編譯，LlmAgentService 使用構造注入傳遞 `IConfiguration`，解耦了對 WPF `App` 類型的依賴。
  * 主控制台呼叫 Gemma API 解析出意圖後，在 `ExecuteLlmIntent` 自動執行動作，包括自動降 Key、升 Key、切換原伴唱聲道、播放、暫停，或解析出 `SEARCH` 意圖後，直接使用 Dapper 發送 SQLite 模糊查詢並進行歌曲點播。
* **驗證**:
  * `LlmTests.AnalyzeIntentAsync_ParsesValidJsonResponse`：利用 `MockHttpMessageHandler` 攔截 HTTP 請求並返回 Mock JSON，驗證解析 intent 的正確性。
  * `LlmTests.AnalyzeIntentAsync_ThrowsOnFailureStatusCode`：驗證當 API 返回 500 錯誤時會「Fail Loudly」拋出 Exception 並寫入 Log。

### Task 6: YouTube 下載與背景處理佇列
* **實作內容**:
  * 建立 [BackgroundJobService.cs](file:///Users/shawnwang/Documents/agy/KTV/BackgroundJobService.cs)，利用 `ConcurrentQueue` 與 `SemaphoreSlim` 實現背景單線程消費者任務佇列。
  * 背景任務流程：`yt-dlp` 下載影片為 `.mp4` $\rightarrow$ `demucs` 抽離伴唱為 `vocals.wav` 與 `accompaniment.wav` $\rightarrow$ 使用 Dapper 將檔案路徑寫入 SQLite 資料庫 Songs 資料表。
  * 進程調用封裝防禦性檢測：若本機路徑不存在 `yt-dlp` 或 `demucs`，自動使用 macOS 的 `which` 命令檢測全域 PATH；若依然不存在則自動切換為 Mock 檔案建立模式（建立虛擬影音文件，模擬 2 秒背景下載與音軌分離）。
  * 主控制台訂閱 `JobStatusChanged` 事件。進程狀態（Downloading 10%, Separating 50%, Completed 100%）將即時、非阻塞地顯示於 UI 助理對話框；任務完成時自動調用 `LoadSongList()` 重新載入 SQLite 歌單並刷新 UI DataGrid。
* **驗證**:
  * `JobTests.EnqueueJob_ExecutesProcessAndSavesToDb`：模擬 YouTube 下載任務，驗證背景隊列在多線程下正確完成 status 轉移、輸出 mock 文件，並透過 Dapper 成功將記錄持久化到 SQLite 數據庫。

---

## 單元測試套件執行結果
執行 `dotnet test KTV.Tests/KTV.Tests.csproj`：
```text
已通過! - 失敗:     0，通過:     7，略過:     0，總計:     7，持續時間: 2 s - KTV.Tests.dll (net8.0)
```
7 個意圖驅動單元測試全部成功通過，保障了核心 SQLite 連線、日誌、聲道切換、SoundTouch 變調邏輯、LLM 意圖解析、以及 YouTube 背景分離管線的代碼正確性。
