# [TASK LIST] 開發迭代任務與檢查點

> **執行準則**: 每完成一個 Task，AI 必須進行 Checkpoint 總結（做了什麼、驗證了什麼、剩什麼）。若 Token 使用量接近 4,000，必須主動摘要並要求重啟對話。

## [x] Task 1: 基礎專案建立與極簡 SQLite 串接
* [x] 建立 .NET 8 WPF 專案，安裝 `Dapper`, `Microsoft.Data.Sqlite`, `Serilog`。
* [x] 撰寫 SQLite 連線邏輯與 `Songs` 資料表建立（程式啟動時 `CREATE TABLE IF NOT EXISTS`）。
* [x] 實作全域異常攔截 (`DispatcherUnhandledException`) 並輸出至日誌。
* **Checkpoint 成功標準**: 程式能順利啟動，資料庫檔案被建立；手動拋出 Exception 時能正確寫入 Log 且不閃退。

## [x] Task 2: 雙螢幕 UI 邏輯實作
* [x] 建立 `ControlPanelWindow` (主螢幕) 與 `PlayerWindow` (延伸螢幕)。
* [x] 實作螢幕偵測邏輯，將 `PlayerWindow` 定位至 `Screen[1]` 並設為無邊框全螢幕。
* **Checkpoint 成功標準**: 在雙螢幕環境執行時，兩個視窗自動分配並定位在正確的螢幕上。

## [x] Task 3: 影音播放 MVP (LibVLCSharp)
* [x] 安裝 `LibVLCSharp.WPF`，於 `PlayerWindow` 嵌入 VLC 播放元件。
* [x] 寫死一個本地 `.mpg` 或 `.mp4` 路徑進行播放測試。
* [x] 實作原唱/伴唱聲道切換 API 呼叫。
* **Checkpoint 成功標準**: 影片正常顯示與發聲，點擊按鈕能成功切換並填滿左右聲道。

## [x] Task 4: 音訊變調管線 (NAudio + SoundTouch)
* [x] 匯入 `SoundTouch` C++ DLL 並設定 P/Invoke 介面。
* [x] 攔截 VLC 音訊輸出 $\rightarrow$ 轉交 `SoundTouch` 處理 $\rightarrow$ 由 `NAudio` (WASAPI) 輸出。
* **Checkpoint 成功標準**: 升降 Key 功能生效，人聲變調不失真，影片嘴型與聲音同步。

## [x] Task 5: LLM API 整合與 Feature Toggle
* [x] 在 `appsettings.json` 讀取 `LlmAgent` 設定。
* [x] 實作 `LlmAgentService`，發送 System Prompt 請求並限制回傳格式為 JSON。
* **Checkpoint 成功標準**: UI 傳送一段測試文字，系統成功解析出 JSON 意圖；若 API 失敗，Fail Loudly 記錄日誌但不可閃退。

## [x] Task 6: YouTube 下載與背景處理佇列
* [x] 建立 `BackgroundJobService` 管理長時間任務佇列。
* [x] 封裝 `YtDlpWrapper` (呼叫 yt-dlp.exe) 與 `VocalRemoverWrapper` (呼叫 demucs.exe)。
* [x] 處理完成後，呼叫資料庫存取寫入 SQLite。
* **Checkpoint 成功標準**: 背景成功下載一部短片並完成去人聲，UI 能顯示進度狀態，且 WPF 介面保持流暢不卡頓。