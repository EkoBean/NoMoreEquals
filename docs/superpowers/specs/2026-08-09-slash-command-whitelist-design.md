# 斜線指令白名單：只在聊天頻道指令上作用

日期：2026-08-09
狀態：設計定案，待實作

## 問題

`ff7a2ed` 讓外掛對所有 `/` 開頭的行都轉換本文，只保留指令詞本身。這對 `/say`、`/p`、`/tell` 是正確的，但對 `/gearset change 綠`、`/target 綠`、以及任何 Dalamud 外掛指令則是錯的 —— 那些參數不是聊天內容，被字形置換改掉會讓指令失效或指到錯的東西。preview 視窗也會在打指令時跳出來干擾。

## 需求

以聊天欄目前的整行內容決定外掛是否作用：

1. 行首**不是** `/` → 現行行為：整行轉換、顯示 preview。
2. 行首是 `/` 且**還沒有半形空格** → 外掛完全不介入。不轉換、不顯示 preview、不攔截 IME 上字。
3. 行首是 `/` 且已有半形空格 → 取出指令詞（`/` 與第一個空格之間），忽略大小寫查白名單：
   - **命中** → 作用，但只作用在空格之後的本文；指令詞本身原封不動。
   - **落空** → 外掛完全不介入，等同第 2 點。

「完全不介入」的具體定義：`TryRewriteResult` 回傳 `null`，因此 `GCS_RESULTSTR` 不被拔除，遊戲照常收到玩家原本輸入的中文。整條 pipeline 不曾碰過那行字。

## 白名單

來源：Lodestone Eorzea Database 與 FFXIV Console Games Wiki，兩份獨立來源互相驗證。共 **60** 個 token（不含開頭的 `/`）。

| 頻道 | 長名 | 短名 |
|---|---|---|
| Say | `say` | `s` |
| Yell | `yell` | `y` |
| Shout | `shout` | `sh` |
| Tell | `tell` | `t` |
| Reply | `reply` | `r` |
| Party | `party` | `p` |
| Alliance | `alliance` | `a` |
| Free Company | `freecompany` | `fc` |
| PvP Team | `pvpteam` | `pt` |
| Novice Network | `novice` | `n` |
| Echo | `echo` | `e` |
| Emote | `emote` | `em` |
| Linkshell（現用） | `linkshell` | `l` |
| Linkshell 1–8 | `linkshell1` … `linkshell8` | `l1` … `l8` |
| CW Linkshell（現用） | `cwlinkshell` | `cwl` |
| CW Linkshell 1–8 | `cwlinkshell1` … `cwlinkshell8` | `cwl1` … `cwl8` |

計數：12 長名 + 12 短名 + 9 linkshell + 9 l 系列 + 9 cwlinkshell + 9 cwl 系列 = 60。

### 刻意排除

- **`quickchat` / `qchat`** —— 參數是預設短語編號，不是自由文字。
- **`beginner` / `b`、`cwls` / `cwls1`–`cwls8`** —— 坊間常見寫法，但 Lodestone 官方資料庫與 Console Games Wiki 皆未列為有效別名。無證據，不加。若日後在遊戲內實測確認可用，再補進表裡。

### 刻意不特別處理

- **`tell` 的收件人 token。** 語法是 `/tell 名 姓@World 訊息`，照規則會連玩家名稱一起送進轉換。實務上無害：FFXIV 玩家名稱只有英文字母，而本外掛的置換邏輯（中文→日文漢字字形、使用者自訂片語）不會命中純英文。

## 架構

### 新增：`Data/ChatChannelCommands.cs`

60 個 token 的唯讀集合，`StringComparer.OrdinalIgnoreCase`。只暴露一個查詢方法：

```csharp
public static bool IsChatChannel(string token);   // token 不含開頭的 '/'
```

放在 `Data/`，與 `KanjiMap`、`KanjiCandidates` 同層 —— 性質相同，都是靜態對照表。

### 新增：`Services/ChatLineScope.cs`

```csharp
internal readonly record struct ChatLineScope(bool Convertible, int BodyStart)
{
    public static ChatLineScope Of(string? line);
}
```

回答完整的問題：這行能不能碰，能碰的話從第幾個字元開始碰。

| 輸入 | 結果 |
|---|---|
| `你好` | `(true, 0)` |
| `/p` | `(false, 0)` |
| `/p 你好` | `(true, 3)` |
| `/gearset change 1` | `(false, 0)` |

取代目前的 `KanjiConverter.GetChatBodyStart` 與 `KanjiConverter.IsInsideCommandToken`。這兩個述詞必須成對呼叫、又可能各自答錯，正是它們得在三處各寫一遍的原因；合成單一型別後，呼叫端拿到的答案不可能自相矛盾。

分隔符**只認半形空格**。`/p　你好`（U+3000 全形空格）判定為「指令詞尚未結束」，因此外掛不作用。這是刻意的：遊戲自己也解析不了全形空格，那行訊息本來就送不出去，外掛沒有理由跟著動。

### 新增：`Services/ConversionGate.cs`

持有 `Configuration`、`KanjiMapService`、`PhraseReplacementService`，暴露兩個成員：

```csharp
public bool IsArmed { get; }              // Enabled 且至少有一條規則可能命中
public bool ShouldConvert(string line);   // IsArmed 且 ChatLineScope.Of(line).Convertible
```

`IsArmed` 的精確定義，與現行 `NoRulesActive` 的否定式等價：

```csharp
configuration.Enabled
    && (mapService.Active.Count > 0 || phraseService.EnabledCount > 0)
```

存在理由是收斂重複：`NoRulesActive` 目前有兩份實作（`ChatInputWatcher.cs:104` 一份，`ChatPreviewOverlay.cs:61` 手寫一份），白名單再進來就會變成三份散落各處的條件。

### 移除

- `KanjiConverter.GetChatBodyStart`
- `KanjiConverter.IsInsideCommandToken`
- `ChatInputWatcher.NoRulesActive`
- `ChatPreviewOverlay.Draw` 中手寫的規則數判斷

`KanjiConverter.ConvertChatLine` 保留，改建在 `ChatLineScope` 上；不可轉換時原字串原樣回傳。它是純函式，即使呼叫端漏問閘門也不會出錯。

### 組裝

`Plugin.cs` 在建立 `chatAccessor` 之前建立 `ConversionGate`，注入 `ChatInputWatcher` 與 `ChatPreviewOverlay`。兩者仍各自持有 `KanjiMapService` 與 `PhraseReplacementService`（轉換時要用真正的對照表）；閘門只取代「該不該做」的判斷。

## 資料流

### 進入點 1：`ChatInputWatcher.TryRewriteResult`

```
現行: !Enabled || !AcceptChatIme || NoRulesActive → null
      IsInsideCommandToken(整行)                  → null

改為: !AcceptChatIme                              → null
      !gate.ShouldConvert(整行)                   → null
```

閘門問的是**整行**，轉換做的是**這次提交的片段** —— 這是現行形狀，不變。整行決定准不准碰，片段才是要轉的東西。`BodyStart` 在此用不到，因為提交的片段永遠落在游標處。

### 進入點 2：`ChatInputWatcher.ConvertBeforeSend`

```csharp
if (!this.gate.IsArmed || this.writing) return;   // 位置不動：必須在 Refresh 之前
this.imeTracker.Refresh(null);
if (this.imeTracker.IsComposing) return;
if (this.imeTracker.HasPendingInsert) return;
// ... 取得 input、讀出 text ...
if (!this.gate.ShouldConvert(text)) return;       // 新增：讀到行內容才問得起
```

`IsArmed` 保留在最前面是刻意的：`Refresh(null)` 會改動組字狀態，往後挪就改變了狀態重算的時機。`ShouldConvert` 無法提前問，因為那時尚未取得行內容。因此 `IsArmed` 會被求值兩次，代價是三個 property 讀取。

### 進入點 3：`ChatPreviewOverlay.Draw`

```csharp
if (!this.gate.IsArmed || !this.configuration.ShowChatPreviewOverlay) return;
// ... 取得 input、rect、raw ...
this.UpdateShadow(raw);                                        // 照常執行
if (!this.gate.ShouldConvert(this.committedShadow)) return;    // 不繪製，但不重設影子
```

兩個決定：

- **判斷對象是 `committedShadow` 而非 `raw`。** 影子才是真正會被繪製的字串；`raw` 是會冒 `=` 雜訊的那個。
- **被擋下時不呼叫 `ResetShadow()`。** 影子必須繼續同步，玩家刪掉 `/gearset ` 的當下 preview 才能在同一幀正確出現。重設會製造閃爍。

## 安全性質：閘門只在入口，不在投遞路徑

`FlushPendingInsert` **不得**呼叫 `ShouldConvert`。

情境：玩家 IME 提交「綠」，原文已從遊戲被拔除，轉換結果排在 insert channel 等待投遞；此時玩家把游標移到行首插入 `/gearset `。閘門翻轉，但那筆 pending insert **必須照樣投遞** —— 它是玩家那幾個字的唯一副本，丟掉就是吃字。

`ChatInputWatcher.cs:210` 現有的 `if (!Enabled || NoRulesActive) converted = original;` 改寫為 `if (!gate.IsArmed) converted = original;`，語意不變：退回原文，而非丟棄。

## 禁令：`SyncSubclass` 不得經過閘門

`ChatInputWatcher.cs:145` 的 `SyncSubclass(this.configuration.Enabled)` **必須維持讀取原始的 `configuration.Enabled`**，不可改成 `gate.IsArmed`。window subclass 是整條 pipeline 存在的前提；一旦讓它依賴其他條件，「規則清空」就會連帶把 RESULTSTR 攔截整個關掉。該處註解已記錄過一次同類教訓（overlay 顯示設定曾經意外關掉轉換）。

## 邊界情況

| 輸入 | 結果 | 說明 |
|---|---|---|
| `""` | 通過 | 空行，沒有東西可轉 |
| `/` | 擋 | 指令詞尚未開始 |
| `/p` | 擋 | 尚無空格 |
| `/p ` | 通過，本文為空 | preview 顯示 `/p `，與現行行為一致 |
| `/p  你好` | 通過，本文 `" 你好"` | 多餘空格屬於本文 |
| `/P 你好` | 通過 | `OrdinalIgnoreCase` |
| `//p 你好` | 擋 | token 為 `/p`，不在白名單 |
| `/ 你好` | 擋 | token 為空字串 |
| `/p　你好`（全形空格） | 擋 | 遊戲不收的指令，外掛不動 |
| `/p ===`（`=` 雜訊） | 通過 | 前綴 `/p ` 全為 ASCII，不受雜訊影響 |
| `/gearset change 1` | 擋 | 不在白名單 |
| `/nme` | 擋 | 本外掛自己的指令也一併被擋，這是正確的 |

### 已知限制（不寫程式處理）

1. 玩家先輸入中文、再回到行首插入 `/gearset `，**已轉換的字不會退回原文**。本外掛從不重寫已提交的文字，這是核心設計約束。
2. 玩家把游標移進指令詞內部再用 IME 上字（例如 `/say` 的 `/` 之後），整行判定為可轉換，該片段會被轉。這行指令本來就會壞掉，不值得為它寫程式。

## 測試

測試專案的 csproj 明訂**只測不依賴 Dalamud 的型別** —— CLR 按型別隨選解析組件，因此沒有遊戲與 Dalamud 安裝也能跑。`Configuration : IPluginConfiguration` 是 Dalamud 型別，所以 `ConversionGate` 不進單元測試層。

這是刻意接受的：`ConversionGate` 只有三行組合邏輯（三個 property 讀取加一次委派），會出錯的東西全在 `ChatLineScope` 與 `ChatChannelCommands` 裡，而那兩個是純函式、零 Dalamud 依賴、可以測到滿。為了讓一個三行的類別可測而把 `Configuration` 換成 `Func<bool>` 注入，是為儀式付錢。

- **`ChatLineScopeTests`**（新）—— 上方邊界表逐列一個 `[InlineData]`，同時驗證 `Convertible` 與 `BodyStart`。
- **`ChatChannelCommandsTests`**（新）—— 60 個 token 全數命中；`linkshell1`–`8`、`l1`–`8`、`cwlinkshell1`–`8`、`cwl1`–`8` 四個編號家族完整（防手打漏字）；`gearset`、`nme`、`quickchat`、`qchat`、`beginner`、`cwls1` 確實落空。
- **`KanjiConverterTests`**（改）—— 刪除 `GetChatBodyStart_*` 與 `IsInsideCommandToken_*` 兩組；新增 `ConvertChatLine_LeavesNonChatCommandAlone`（`/gearset 綠` 原樣回傳）；保留 `ConvertChatLine_ConvertsCommandArgumentsButNotTheCommand`、`ConvertChatLine_ConvertsAnOrdinaryLineWhole`、`ConvertChatLine_AppliesPhrasesBeforeGlyphs`。

## 範圍之外

**自訂字典的輸入驗證**（禁止純 ASCII 的來源側規則）在討論中提出並刻意延後。它是設定視窗的輸入驗證，與 IME pipeline 零交集，應另立一份 spec。

## 來源

- [Eorzea Database: Text Commands — The Lodestone](https://na.finalfantasyxiv.com/lodestone/playguide/db/text_command/)
- [Chat text commands — FFXIV Console Games Wiki](https://ffxiv.consolegameswiki.com/wiki/Chat_text_commands)
- [Text commands — FFXIV Console Games Wiki](https://ffxiv.consolegameswiki.com/wiki/Text_commands)
