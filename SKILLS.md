# AI 開發編碼準則 (Coding Principles)

## 核心原則

### Rule 1: Think Before Coding (先想再寫)
*   不要做隱性假設，明確說明假設前提。
*   面對 Trade-off 時應展開討論。
*   不確定時直接詢問，不要猜測。
*   存在更簡單的做法時，應反對複雜方案。

### Rule 2: Simplicity First (先求簡單)
*   撰寫能解決問題的最小程式碼。
*   不寫推測性功能，不為一次性程式碼建立抽象層。
*   資深工程師的標準：過於複雜的設計必須簡化。

### Rule 3: Surgical Changes (外科式修改)
*   只變動該動的地方，避免「順手改善」相鄰程式碼、註解或格式。
*   不重構沒有損壞的東西。
*   必須配合既有的程式風格。

### Rule 4: Goal-Driven Execution (目標導向執行)
*   定義成功標準，並迭代至驗證通過為止。
*   專注於描述「成功的樣貌」，而非具體步驟。

### Rule 5: Task Separation (任務拆分)
*   **Claude:** 用於需要判斷的任務（分類、起草、摘要、抽取）。
*   **程式碼:** 處理確定性決策（重試、路由、狀態碼處理、確定性轉換）。

---

## 執行與效能

### Rule 6: Token Budget Management (預算管理)
*   單次任務上限 4,000 tokens。
*   單次 Session 上限 30,000 tokens。
*   接近上限時主動進行摘要與重啟，嚴禁無聲突破。

### Rule 7: Pattern Selection (模式選擇)
*   衝突模式中必須「二選一」（傾向較新、較有測試的）。
*   解釋選擇原因，並將另一個模式標記為待清理，嚴禁混合模式。

### Rule 8: Code Understanding (先讀後寫)
*   撰寫前需讀懂：Exports、直接 Caller、共用 Utility。
*   嚴禁使用「看起來無關 (looks orthogonal)」等不明確措辭。

---

## 驗證與溝通

### Rule 9: Intent-Based Testing (意圖驗證)
*   測試需驗證「意圖」而非僅「行為」。
*   合格測試標準：當業務邏輯改變時，測試必須失敗。

### Rule 10: Multi-Step Checkpoints (多步驟檢核)
*   每完成一個子步驟即進行總結：已完成事項、已驗證事項、剩餘事項。
*   無法清楚描述狀態時，嚴禁繼續下一步。

### Rule 11: Adhere to Conventions (配合慣例)
*   嚴格遵守既有 Codebase 慣例（不論命名風格或架構）。
*   若不認同，應另行討論，而非單方面進行分叉修改。

### Rule 12: Fail Loudly (失敗要大聲)
*   誠實揭露狀態：若跳過任何數據或測試，不得聲稱「已完成」。
*   主動揭露不確定性，嚴禁隱藏問題。
