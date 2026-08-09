# 自訂字典來源側驗證：必須含中文字

日期：2026-08-10
狀態：設計定案，待實作

## 問題

自訂片語的來源側是使用者自己打的任意字串，沒有任何限制。純英數的規則（`abc` → `某某`）跟這個插件的工作完全無關，卻會對每一行聊天生效，等於把一個字形轉換插件當成通用文字取代工具在用。

具體損害在 [2026-08-09-slash-command-whitelist-design.md](2026-08-09-slash-command-whitelist-design.md) 已記錄：一條 `From = "Alt"` 的規則會把 `/tell Alt Ego@World` 的收件人改掉，讓悄悄話送不出去。字形對照表不可能有這問題（它只有中文→日文漢字，碰不到英文字母），但片語可以。

Missing-glyph mapping 目前只限制「兩邊各剛好一個字」，同樣擋不掉 `a` → `b`。

## 需求

**來源側（`From`）必須至少含一個中文字。** 四個決定：

| 決定 | 選擇 |
|---|---|
| 套用哪一側 | 只有 `From`。`To` 不限制 —— 你想換成什麼都行，包含空字串（等於刪掉） |
| 嚴格程度 | **至少含一個**，不是「每個字元都要是」。擋掉的是「跟這個插件無關的規則」，不是「不夠純粹的中文」 |
| 執行點 | 只在設定視窗。按下「新增」時擋下並顯示錯誤 |
| 套用範圍 | Missing-glyph mapping 與 custom phrases 兩者 |

`Rebuild` 不過濾，所以手改 `NoMoreEquals.json` 可以繞過，而升級後舊設定裡的既有規則不會無聲失效。這是刻意的取捨：靜默停止作用比擋不住手動編輯更糟。

### 通過與擋下的例子

| 來源側 | 結果 |
|---|---|
| `綠色` | 通過 |
| `綠，` | 通過（含一個漢字就夠） |
| `ㄅㄅ` | 通過（注音） |
| `a好` | 通過 |
| `abc` | 擋 |
| `123` | 擋 |
| `!!!` | 擋 |
| `   ` | 擋 |
| `あ` | 擋（假名不是中文字） |

## 判準

「中文字」= **漢字或注音**。

漢字取 Unicode 的 Han script：

| 區段 | 範圍 |
|---|---|
| 擴充 A | U+3400–U+4DBF |
| 基本區 | U+4E00–U+9FFF |
| 相容區 | U+F900–U+FAFF |
| 擴充 B 以後 | U+20000–U+3FFFF |

用 Han script 而不是「中文字」是刻意的：中文漢字與日文漢字在 Unicode 裡是**同一批碼位**，程式分不出來也不需要分。`綠` 與 `緑` 都是 Han。

注音沿用既有的 `ChatBufferHeuristics.IsBopomofo`（注音符號、注音擴充、五個聲調符號），**不另外定義一份**。同一個概念在同一個 codebase 裡有兩份定義，遲早會漂移。

納入注音的理由：`ㄅㄅ`、`ㄏㄏ`、`ㄋㄋ` 是台灣玩家日常在用的寫法，而注音符號正是 AXIS 渲染不出來、會變成 `=` 的字 —— 那本來就是這個插件該處理的東西。

## 架構

### 新增：`Services/ChineseScript.cs`

```csharp
public static bool HasChineseCharacter(string? text);
```

純函式，零 Dalamud 依賴，用 `Rune` 逐字掃（擴充區的代理對才算得對）。

### 接線：`Windows/Tabs/MainTab.cs`

- `TryAddPhrase` —— `From` 沒有中文字就擋下並設定錯誤訊息
- `TryAddGlyph` —— 解析出的來源字不是中文字就擋下

兩處都是加進既有的 guard clause 串列，不新增巢狀結構。

### 新增 i18n 字串：`SourceNeedChineseError`

兩處共用，因為理由一樣。

- tw：`原文必須包含中文字，這個插件只轉換中文字形。`
- en：`From must contain at least one Chinese character.`

## 測試

`ChineseScriptTests`（新）—— 上方例子表逐列一個 `[InlineData]`，另加：

- 四個漢字區段各取一個代表字（含擴充 B 的代理對，驗證 `Rune` 走法正確）
- 注音符號、注音擴充、聲調符號各一
- `null` 與空字串

`MainTab` 的接線不進單元測試層：它碰 ImGui 與 `Configuration`，而測試專案只測不依賴 Dalamud 的型別。可驗證的邏輯全在 `ChineseScript` 裡。

## 範圍之外

- **`To` 側的驗證。** 不做。
- **`Rebuild` 過濾。** 不做，理由見上。
- **既有規則的遷移或標示。** 不做。舊規則照常生效。
