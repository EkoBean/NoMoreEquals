# 預設自訂片語與「加入預設條目」按鈕

日期：2026-08-10
狀態：設計定案，待實作

## 問題

`CommonParticles` 讓 missing-glyph mapping 一開始就有幾筆可用的預設（啊→阿、嗎→嘛、喔→哦），由 `DefaultGlyphSeeder` 在啟動時種進 `Configuration.CustomMappings`。Custom phrases 沒有對應的東西 —— 使用者第一次打開設定視窗，片語清單是空的。

兩個缺口：

1. **預設片語不存在。** 多字詞的替換（咖啡→珈琲）沒有出貨管道，每個使用者都要自己從零打。
2. **被刪掉的預設救不回來。** `SeededDefaultGlyphKeys` 的設計刻意讓刪掉的預設不復活，但沒有任何手段可以反悔。誤刪就是永久的，除非手改 `NoMoreEquals.json`。

## 需求

| 決定 | 選擇 |
|---|---|
| 預設片語如何呈現 | 種進 `Configuration.PhraseReplacements`，與 glyph 完全相同的模式。使用者可編輯、可停用、可刪除，刪掉不復活 |
| 資料來源形式 | 純 C# data class，寫在原始碼裡。**不用** JSON、不用產生器、不用嵌入資源 |
| 分類方式 | 由檔案決定：單字進 `CommonParticles`，多字詞進 `CommonPhrases`。編譯器強制（`char` vs `string`） |
| 補回機制 | Advanced 分頁一顆按鈕，一次跑 glyph 與 phrase 兩類 |
| 補回語意 | **只增不改**。不覆寫 `To`、不動 `Enabled` |
| 字面寫法 | 沿用 Unicode escape + 中文註解，同 `CommonParticles` 現況 |

### 為什麼不用 JSON

設計過程中評估過三條路：JSON + 建置期產生器、JSON 嵌入資源執行期解析、純 C# data class。選最後一個。

JSON 的價值在於讓非開發者編輯、或讓外部程式讀取。**這兩件事都不需要** —— 這份字庫只有維護者會改，而維護者本來就在寫 C#。引入 JSON 等於為一個不存在的需求，付出產生器（改完要記得跑，忘了跑會靜默失效）或執行期解析（格式錯誤要到執行期才發現）的成本。

純 data class 同時拿到「改完直接生效」與「編譯期抓錯」，且零額外流程。

### 為什麼兩個檔案不算「拆分」

需求是「新增時不用在兩套結構之間切換」，不是「物理上同一個檔案」。兩個檔案的寫法完全一致（都是 `Entries` 字典、都是 `["從"] = "到"`），並排在同一個資料夾，差別只有 `char` 與 `string`。

而這個差別是**資產不是負擔**：把 `"咖啡"` 寫進 `char` 字典是編譯錯誤，當場紅字。單一來源加執行期長度分流反而會失去這道保護。

## 架構

### 資料層

資料夾 `Data/DefaultMissingGlyphs/` 改名為 `Data/Defaults/`，namespace 同步改為 `NoMoreEquals.Data.Defaults`。改名理由：`DefaultMissingGlyphs` 語意裝不下 phrase，而現在只有 3 個引用點（2 個 `namespace` 宣告、1 個 `using`），是成本最低的時機。

```
NoMoreEquals/Data/Defaults/
  CommonParticles.cs      單字，char → char（現有，僅改 namespace）
  CommonPhrases.cs        多字詞，string → string（新增）
  DefaultGlyphMaps.cs     彙整 glyph 模組（由 DefaultMissingGlyphMaps 改名）
  DefaultPhraseMaps.cs    彙整 phrase 模組（新增）
```

`DefaultPhraseMaps` 的形狀比照 `DefaultGlyphMaps`：一份 `Modules` 清單、一個 `Merge`、保留 `from != to` 的防呆。維持「寫一個模組、加進 `Modules`」的既有流程。

`CommonPhrases` 初始內容為 咖啡 → 珈琲。此條目已定案，不需驗證 AXIS 覆蓋率。

### Configuration

新增 `SeededDefaultPhraseKeys`（`List<string>`，以 `From` 為 key），與現有的 `SeededDefaultGlyphKeys` 平行。`Normalize()` 補對應的 `??=`。`Version` 由 3 升為 4 —— 依現有註解，只新增有預設值的欄位不需要 migration 分支，蓋版本號即可。

**`SeededDefaultGlyphKeys` 維持原名。** 它是持久化欄位，改名會使舊 config 的值對不上，反序列化後成為空清單，結果是所有使用者刪掉的預設一次全部復活。namespace 與類別名是純編譯期的東西，改名零風險；持久化欄位名不是。這個不對稱是刻意的。

### 兩條種入路徑

這是本設計的核心。自動種入與按鈕**必須**行為不同，因為 `SeededDefaultPhraseKeys` 存在的目的正是阻止刪掉的預設復活，而按鈕的用途正是把它們找回來。

| | 觸發 | 查 seeded-keys | 遇到已存在的 key |
|---|---|---|---|
| **自動種入** `SeedInto` | 外掛啟動 | **查** —— 已提供過則跳過 | 不動 |
| **加入預設** `RestoreAll` | 使用者點擊 | **不查** —— 刻意忽略 | 不動 |

兩者都**只增不改**：不覆寫 `To`、不動 `Enabled`。因此按鈕非破壞性，不需確認對話框。

`RestoreAll` 一樣寫入 seeded-keys，確保日後新出的預設仍能被下次自動種入正確處理。

兩個 seeder 各自提供 `SeedInto` 與 `RestoreAll`，共用插入邏輯，唯一差異是是否查詢 seeded-key 集合。

### 收據清單的必要性

`SeededDefaultGlyphKeys` 記錄「哪些預設已經送給這個使用者」。沒有它，seeder 每次啟動都會發現 `CustomMappings` 裡缺了 啊 就補回去 —— 預設變成刪不掉。

它不能簡化為單一 `bool`（「這台機器種過了」），否則日後新增第四筆預設時，老使用者永遠拿不到。逐筆記錄才能同時做到「舊的不復活、新的照給」。

此機制與 `UseLiveFontFilter` / `AxisFontCoverage` 那條「掃描 AXIS 字型決定哪些字畫不出來」的路徑無關，兩者沒有交集。

### UI

Advanced 分頁一顆按鈕，一次跑兩類，回報加入筆數（「已加入 N 筆」／「沒有可加入的項目」）。

放 Advanced 而非 Main 的理由：這是低頻的補救操作，Main 分頁的兩欄已被 Add 按鈕與清單佔滿。既有的 `RebuildFromFont`（[AdvancedTab.cs:42](../../../NoMoreEquals/Windows/Tabs/AdvancedTab.cs#L42)）是同性質操作的先例。

新增 `UiStrings` 欄位與 `LocEn`／`LocTw` 字串。按鈕須呼叫 context 的 rebuild-and-save，使變更立即生效。

按鈕文字用「加入預設條目」而非「還原預設」—— 它不會把已編輯的條目還原成預設值，只補缺少的。用「還原」會讓使用者期待一個它不執行的重置。

## 測試

`tests/` 目前沒有 seeder 覆蓋。新增 `DefaultPhraseSeederTests`，涵蓋四個行為：

| 案例 | 預期 |
|---|---|
| 全新 config 自動種入 | 預設片語寫入 `PhraseReplacements`，key 記入 `SeededDefaultPhraseKeys` |
| 刪除後再次自動種入 | 不復活 |
| 刪除後按下按鈕 | 加回來 |
| 已編輯 `To` 後按下按鈕 | 保留使用者的 `To`，不覆寫 |

## 改動清單

**改名**
- `Data/DefaultMissingGlyphs/` → `Data/Defaults/`
- namespace `NoMoreEquals.Data.DefaultMissingGlyphs` → `NoMoreEquals.Data.Defaults`（2 檔）
- `DefaultGlyphSeeder.cs` 的 `using`（1 行）
- 類別 `DefaultMissingGlyphMaps` → `DefaultGlyphMaps`（含引用點）

**新增**
- `Data/Defaults/CommonPhrases.cs`
- `Data/Defaults/DefaultPhraseMaps.cs`
- `Services/DefaultPhraseSeeder.cs`
- `tests/NoMoreEquals.Tests/DefaultPhraseSeederTests.cs`

**修改**
- `Configuration.cs`：加 `SeededDefaultPhraseKeys`、`Normalize()` 補 `??=`、`Version` 3→4
- `Services/DefaultGlyphSeeder.cs`：加 `RestoreAll`
- `Plugin.cs`：啟動時多呼叫一次 phrase 種入，兩個 bool 合併為一次 `Save()`
- `Windows/Tabs/AdvancedTab.cs`：按鈕
- `Localization/UiStrings.cs`、`LocEn.cs`、`LocTw.cs`：按鈕與結果訊息字串

## 測試環境限制

`Configuration` 實作 Dalamud 的 `IPluginConfiguration`，**在測試專案中無法建構** —— 一經實體化即拋出 `FileNotFoundException: Could not load file or assembly 'Dalamud'`，因為測試環境沒有 Dalamud。此點已於 2026-08-10 以探測測試實證。

因此 seeder 的核心邏輯**不得**以 `Configuration` 為參數，須改收它實際需要的資料：

```csharp
// 核心邏輯，Dalamud-free，測試直接呼叫
public static bool Seed(
    List<PhraseReplacement> phrases,
    List<string> seededKeys,
    bool ignoreSeededKeys);

// 薄包裝，正式程式碼用，測試不碰
public static bool SeedInto(Configuration config);
public static bool RestoreAll(Configuration config);
```

`ignoreSeededKeys` 恰好對應「兩條種入路徑」那張表的差異，一個參數表達完畢。

這不是為測試而扭曲設計，而是套用專案既有慣例 —— `PhraseReplacementService.Rebuild` 的註解已明載相同理由：「Takes the rules rather than the whole `Configuration` so this stays independent of Dalamud's `IPluginConfiguration` and can be exercised directly」。

`DefaultGlyphSeeder` 加 `RestoreAll` 時套用同一拆法，順帶使其可測。
