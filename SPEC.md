# [SPEC] KTV 點歌與播放系統：AI 增強版最小可行性規格書

## 1. 專案概述 (Overview)
本專案為基於 **.NET 8.0 WPF** 打造的 KTV 點歌與播放系統 MVP。系統嚴守「Simplicity First (先求簡單)」與「Goal-Driven (目標導向)」準則。核心聚焦於雙螢幕展示、本地影音解碼、即時變調，並透過功能開關（Feature Toggles）引入「LLM 語意解析點歌」與「YouTube 動態擴充與 AI 去人聲」兩大進階模組。

## 2. 系統假設與架構決策 (Assumptions & Trade-offs)
* **資料庫**: 採用純文字 SQLite (`Microsoft.Data.Sqlite` + `Dapper`)。不建立複雜的 Repository/UnitOfWork 抽象層，直接由 Service 呼叫 Dapper 以求極簡。
* **影音解碼**: `LibVLCSharp.WPF`。假設目標機器為 Windows 11 且支援硬體解碼 (DXVA2)。
* **音訊與變調**: `SoundTouch` (C++ DLL) 搭配 `NAudio` (WASAPI)。以 30ms 延遲為成功標準。
* **UI 狀態管理**: 單純的 MVVM，僅使用內建的 `CommunityToolkit.Mvvm`。
* **LLM 整合**: 假設外部或本地端已有可用的 Gemma 2B API。C# 僅負責發送 Prompt 並解析 JSON 意圖，不負責模型推理。
* **YouTube 與 AI 去人聲**: 為避免系統臃腫，強依賴外部工具 (`yt-dlp.exe`, `demucs.exe`)。採用背景佇列 (Background Queue) 處理，確保不阻塞 UI。

## 3. 核心功能目標 (Goal-Driven Features)

### 3.1 基礎點播與控制 (Base KTV Logic)
* **雙螢幕配置**: 點歌介面固定於主螢幕；播放介面自動於延伸螢幕全螢幕顯示 (無邊框)。
* **聲道切換 (原/伴唱)**: 支援 MPG/MP4。點擊按鈕立刻執行左右聲道複製 ($L \rightarrow L, L \rightarrow R$ 或 $R \rightarrow L, R \rightarrow R$)，無爆音。
* **即時變調 (Pitch)**: 變調範圍 $\pm 6$ 半音 (演算法參考：$f_{out} = f_{in} \times 2^{\frac{k}{12}}$，其中 $k$ 為半音數)。聲音不可失真，影音同步誤差 $\le \pm 15$ ms。

### 3.2 AI 增強模組 (AI Augmented Modules)
* **LLM 語音/文字代理 (LLM Agent)**: 送出自然語言後，系統能透過 Gemma API 正確解析出查詢意圖，並轉化為安全的 SQLite 查詢條件。
* **網路歌庫擴充工作站 (Cloud-to-Local Worker)**: 
  * 流程：搜尋 $\rightarrow$ 呼叫 `yt-dlp` 下載 $\rightarrow$ 呼叫 `demucs` 抽離伴奏 $\rightarrow$ 寫入 SQLite。
  * 目標：過程不可阻塞 WPF 執行緒，完成後自動更新歌單。

## 4. 品質與測試準則 (Quality & Testing)
* **Fail Loudly (失敗要大聲)**: 全域異常必須被攔截並完整記錄 StackTrace，嚴禁使用空的 `catch` 隱藏錯誤。若 API 呼叫失敗或影片載入失敗，必須在 UI 明確跳出提示。
* **意圖驅動測試**: 測試必須驗證業務邏輯意圖（如：`When_Pitch_Up_Expect_Semitone_Increased`），而非單純驗證行為。