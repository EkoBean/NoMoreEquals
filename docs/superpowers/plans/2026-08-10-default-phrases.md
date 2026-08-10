# 預設自訂片語與「加入預設條目」按鈕 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 讓 custom phrases like missing-glyph mapping 一樣有出貨的預設條目，並提供一顆按鈕把被刪掉的預設補回來（glyph 與 phrase 兩類）。

**Architecture:** 資料層新增 `CommonPhrases`／`DefaultPhraseMaps`，與既有的 glyph 模組並排於改名後的 `Data/Defaults/`。Seeder 的核心邏輯改收純資料參數（非 `Configuration`），以 `ignoreSeededKeys` 布林區分「啟動自動種入」與「使用者按按鈕」兩條路徑，兩者皆只增不改。UI 在 Advanced 分頁加一顆按鈕，一次跑兩類。

**Tech Stack:** C# / .NET 10 (net10.0-windows)、xUnit 2.9.2、Dalamud plugin SDK、ImGui (Dalamud.Bindings.ImGui)

設計文件：[2026-08-10-default-phrases-design.md](../specs/2026-08-10-default-phrases-design.md)

## Global Constraints

- **中文字面一律用 Unicode escape**，可讀性靠行末註解。範例：`['\u554A'] = '\u963F', // 啊 -> 阿`。理由：`.cs` 檔編碼／BOM 在不同編輯器與 git 設定下不可靠，`\uXXXX` 是純 ASCII。
- **測試不得建構 `Configuration`。** 它實作 Dalamud 的 `IPluginConfiguration`，實體化會拋 `FileNotFoundException: Could not load file or assembly 'Dalamud'`（已實證）。測試只能碰 Dalamud-free 型別：`PhraseReplacement`、`List<string>`、`Dictionary<string, string>`。
- **`SeededDefaultGlyphKeys` 這個持久化欄位名不得更改。** 改名會使舊 config 的值對不上，反序列化成空清單，導致所有使用者刪掉的預設一次全部復活。namespace 與類別名可自由改（純編譯期）。
- **種入語意一律「只增不改」**：不覆寫既有條目的 `To`，不改動 `Enabled`。
- 專案根目錄為 `d:\Work\Coding\project\NoMoreEquals`。所有 `dotnet` 指令在根目錄執行。
- 現有測試基線為 **132 passed**，每次跑測試都不得低於此數。
- **不要自動 commit。** 每個 Task 結尾的「驗證」步驟取代 commit，由維護者自行決定何時提交。

---

## File Structure

| 檔案 | 責任 | 動作 |
|---|---|---|
| `NoMoreEquals/Data/Defaults/CommonParticles.cs` | 單字預設（`char`→`char`） | 移動＋改 namespace |
| `NoMoreEquals/Data/Defaults/DefaultGlyphMaps.cs` | 彙整 glyph 模組 | 移動＋改名＋改 namespace |
| `NoMoreEquals/Data/Defaults/CommonPhrases.cs` | 多字詞預設（`string`→`string`） | 新增 |
| `NoMoreEquals/Data/Defaults/DefaultPhraseMaps.cs` | 彙整 phrase 模組 | 新增 |
| `NoMoreEquals/Services/DefaultGlyphSeeder.cs` | glyph 種入，核心邏輯可測 | 重構＋加 `RestoreAll` |
| `NoMoreEquals/Services/DefaultPhraseSeeder.cs` | phrase 種入，核心邏輯可測 | 新增 |
| `NoMoreEquals/Configuration.cs` | 持久化資料 | 加欄位、升版本 |
| `NoMoreEquals/Plugin.cs` | 啟動時種入 | 多呼叫一次 |
| `NoMoreEquals/Windows/Tabs/AdvancedTab.cs` | 按鈕 UI | 加按鈕與結果訊息 |
| `NoMoreEquals/Localization/{UiStrings,LocEn,LocTw}.cs` | 介面字串 | 加 3 個字串 |
| `tests/NoMoreEquals.Tests/DefaultPhraseSeederTests.cs` | phrase seeder 行為 | 新增 |
| `tests/NoMoreEquals.Tests/DefaultGlyphSeederTests.cs` | glyph seeder 行為 | 新增 |

Task 順序刻意由內而外：資料層 → seeder → config 接線 → UI。每個 Task 結束時專案都能編譯、測試都綠。

---

### Task 1: 資料夾與 namespace 改名

先做這一步，後續所有檔案才能落在正確位置。此 Task 不改變任何行為，純結構調整。

**Files:**
- Move: `NoMoreEquals/Data/DefaultMissingGlyphs/CommonParticles.cs` → `NoMoreEquals/Data/Defaults/CommonParticles.cs`
- Move+Rename: `NoMoreEquals/Data/DefaultMissingGlyphs/DefaultMissingGlyphMaps.cs` → `NoMoreEquals/Data/Defaults/DefaultGlyphMaps.cs`
- Modify: `NoMoreEquals/Services/DefaultGlyphSeeder.cs:2,22`

**Interfaces:**
- Consumes: 無（起始 Task）
- Produces: namespace `NoMoreEquals.Data.Defaults`；類別 `NoMoreEquals.Data.Defaults.DefaultGlyphMaps` 具靜態屬性 `Entries`，型別 `IReadOnlyDictionary<char, char>`

- [ ] **Step 1: 用 git mv 移動兩個檔案**

```bash
cd /d/Work/Coding/project/NoMoreEquals
mkdir -p NoMoreEquals/Data/Defaults
git mv NoMoreEquals/Data/DefaultMissingGlyphs/CommonParticles.cs NoMoreEquals/Data/Defaults/CommonParticles.cs
git mv NoMoreEquals/Data/DefaultMissingGlyphs/DefaultMissingGlyphMaps.cs NoMoreEquals/Data/Defaults/DefaultGlyphMaps.cs
rmdir NoMoreEquals/Data/DefaultMissingGlyphs
```

用 `git mv` 而非刪除重建，git 才追得到改名歷史。

- [ ] **Step 2: 改 `CommonParticles.cs` 的 namespace**

第 3 行由：

```csharp
namespace NoMoreEquals.Data.DefaultMissingGlyphs;
```

改為：

```csharp
namespace NoMoreEquals.Data.Defaults;
```

- [ ] **Step 3: 改 `DefaultGlyphMaps.cs` 的 namespace、類別名與註解**

第 3 行 namespace 同上改為 `NoMoreEquals.Data.Defaults;`。

類別宣告由 `internal static class DefaultMissingGlyphMaps` 改為 `internal static class DefaultGlyphMaps`。

XML 註解中的 `<see cref="Services.DefaultGlyphSeeder"/>` 保持不變（仍正確）。註解裡「To add more defaults: create a module (like `CommonParticles`), then append its `Entries` to `Modules`.」保持不變。

- [ ] **Step 4: 改 `DefaultGlyphSeeder.cs` 的 using 與引用**

第 2 行由 `using NoMoreEquals.Data.DefaultMissingGlyphs;` 改為 `using NoMoreEquals.Data.Defaults;`。

第 22 行 `foreach (var (from, to) in DefaultMissingGlyphMaps.Entries)` 改為 `DefaultGlyphMaps.Entries`。

- [ ] **Step 5: 確認沒有遺漏的引用**

```bash
cd /d/Work/Coding/project/NoMoreEquals
grep -rn "DefaultMissingGlyph" --include=*.cs .
```

預期：**沒有任何輸出**。有輸出就是漏改，逐一修掉。

- [ ] **Step 6: 建置並跑測試**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet build && dotnet test
```

預期：建置成功，`Passed! - Failed: 0, Passed: 132`。純改名不應影響任何測試。

---

### Task 2: CommonPhrases 與 DefaultPhraseMaps 資料模組

**Files:**
- Create: `NoMoreEquals/Data/Defaults/CommonPhrases.cs`
- Create: `NoMoreEquals/Data/Defaults/DefaultPhraseMaps.cs`

**Interfaces:**
- Consumes: namespace `NoMoreEquals.Data.Defaults`（Task 1）
- Produces: `NoMoreEquals.Data.Defaults.DefaultPhraseMaps.Entries`，型別 `IReadOnlyDictionary<string, string>`；`CommonPhrases.Entries` 同型別

- [ ] **Step 1: 建立 `CommonPhrases.cs`**

```csharp
using System.Collections.Generic;

namespace NoMoreEquals.Data.Defaults;

/// <summary>
/// Multi-character wording defaults seeded into the user's phrase replacements.
/// Unlike <see cref="CommonParticles"/> these go through
/// <see cref="Services.PhraseReplacementService"/>, so each entry gets its own
/// enable toggle and participates in longest-match-first replacement.
/// </summary>
internal static class CommonPhrases
{
    public static IReadOnlyDictionary<string, string> Entries { get; } = new Dictionary<string, string>
    {
        ["\u5496\u5561"] = "\u73C8\u7432", // 咖啡 -> 珈琲
    };
}
```

- [ ] **Step 2: 建立 `DefaultPhraseMaps.cs`**

形狀刻意比照 `DefaultGlyphMaps`，包含 `from != to` 防呆。

```csharp
using System.Collections.Generic;

namespace NoMoreEquals.Data.Defaults;

/// <summary>
/// Shipped multi-character wording defaults.
/// <see cref="Services.DefaultPhraseSeeder"/> copies these into the user's editable
/// phrase replacements so they appear on the Main tab like any other customization.
/// <para>
/// To add more defaults: create a module (like <see cref="CommonPhrases"/>),
/// then append its <c>Entries</c> to <see cref="Modules"/>.
/// </para>
/// </summary>
internal static class DefaultPhraseMaps
{
    /// <summary>Curated packs. Append new module <c>Entries</c> here when adding defaults.</summary>
    private static readonly IReadOnlyList<IReadOnlyDictionary<string, string>> Modules =
    [
        CommonPhrases.Entries,
    ];

    /// <summary>All curated default pairs (later modules override earlier on key clash).</summary>
    public static IReadOnlyDictionary<string, string> Entries { get; } = Merge(Modules);

    private static Dictionary<string, string> Merge(IReadOnlyList<IReadOnlyDictionary<string, string>> modules)
    {
        var map = new Dictionary<string, string>();
        foreach (var module in modules)
        {
            foreach (var (from, to) in module)
            {
                if (from != to)
                    map[from] = to;
            }
        }

        return map;
    }
}
```

- [ ] **Step 3: 建置**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet build
```

預期：成功。（兩個類別目前還沒有消費者，`DefaultPhraseSeeder` 於 Task 3 建立；`internal static` 未被引用不會產生警告。）

---

### Task 3: DefaultPhraseSeeder（TDD）

本 Task 是整個計畫的核心。先寫測試，因為種入語意（只增不改、兩條路徑）正是最容易寫錯的地方。

**Files:**
- Create: `tests/NoMoreEquals.Tests/DefaultPhraseSeederTests.cs`
- Create: `NoMoreEquals/Services/DefaultPhraseSeeder.cs`

**Interfaces:**
- Consumes: `DefaultPhraseMaps.Entries`（Task 2）；`PhraseReplacement`（既有，具 `From`／`To`／`Enabled` 三個可設屬性）
- Produces:
  - `DefaultPhraseSeeder.Seed(List<PhraseReplacement> phrases, List<string> seededKeys, bool ignoreSeededKeys) -> bool`
  - `DefaultPhraseSeeder.SeedInto(Configuration config) -> bool`
  - `DefaultPhraseSeeder.RestoreAll(Configuration config) -> bool`

- [ ] **Step 1: 寫失敗的測試**

`Seed` 是 `internal`，但 `NoMoreEquals.csproj:16` 已有 `<InternalsVisibleTo Include="NoMoreEquals.Tests" />`（已確認），測試可直接呼叫，不需任何額外設定。

建立 `tests/NoMoreEquals.Tests/DefaultPhraseSeederTests.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;
using NoMoreEquals.Data.Defaults;
using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

/// <summary>
/// Exercises the Dalamud-free core. <see cref="Configuration"/> cannot be constructed
/// here — it implements Dalamud's IPluginConfiguration and instantiating it throws
/// FileNotFoundException for the Dalamud assembly — so these call Seed directly.
/// </summary>
public class DefaultPhraseSeederTests
{
    /// <summary>A key that is actually shipped, so the tests move if the data changes.</summary>
    private static string FirstDefaultKey => DefaultPhraseMaps.Entries.Keys.First();

    private static string FirstDefaultValue => DefaultPhraseMaps.Entries[FirstDefaultKey];

    [Fact]
    public void Seed_PopulatesEmptyConfigAndRecordsKeys()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();

        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        Assert.True(changed);
        Assert.Equal(DefaultPhraseMaps.Entries.Count, phrases.Count);
        var rule = phrases.Single(p => p.From == FirstDefaultKey);
        Assert.Equal(FirstDefaultValue, rule.To);
        Assert.True(rule.Enabled);
        Assert.Contains(FirstDefaultKey, seeded);
    }

    [Fact]
    public void Seed_DoesNotResurrectADeletedDefault()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();
        DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        // The user deletes it, then the plugin restarts.
        phrases.RemoveAll(p => p.From == FirstDefaultKey);
        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        Assert.False(changed);
        Assert.DoesNotContain(phrases, p => p.From == FirstDefaultKey);
    }

    [Fact]
    public void RestoreAll_AddsBackADeletedDefault()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();
        DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);
        phrases.RemoveAll(p => p.From == FirstDefaultKey);

        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: true);

        Assert.True(changed);
        var rule = phrases.Single(p => p.From == FirstDefaultKey);
        Assert.Equal(FirstDefaultValue, rule.To);
    }

    [Fact]
    public void RestoreAll_PreservesAUserEditedValue()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();
        DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        // The user rewrites the target and switches the rule off.
        var edited = phrases.Single(p => p.From == FirstDefaultKey);
        edited.To = "\u6E2C\u8A66"; // 測試
        edited.Enabled = false;

        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: true);

        Assert.False(changed);
        Assert.Equal("\u6E2C\u8A66", edited.To);
        Assert.False(edited.Enabled);
        Assert.Single(phrases, p => p.From == FirstDefaultKey);
    }
}
```

四個測試對應 spec 那張表的四列。刻意用 `FirstDefaultKey` 而非硬寫「咖啡」，這樣日後改資料不會弄壞測試。

- [ ] **Step 2: 跑測試確認失敗**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet test --filter "FullyQualifiedName~DefaultPhraseSeederTests"
```

預期：**編譯失敗**，訊息類似 `error CS0246: The type or namespace name 'DefaultPhraseSeeder' could not be found`。這是正確的失敗 —— 實作還不存在。

- [ ] **Step 3: 寫最小實作**

建立 `NoMoreEquals/Services/DefaultPhraseSeeder.cs`：

```csharp
using System;
using System.Collections.Generic;
using NoMoreEquals.Data.Defaults;

namespace NoMoreEquals.Services;

/// <summary>
/// Copies shipped phrase defaults into the user's editable replacements.
/// </summary>
internal static class DefaultPhraseSeeder
{
    /// <summary>
    /// Seed defaults into <see cref="Configuration.PhraseReplacements"/> on startup.
    /// Skips anything already offered, so a default the user deleted stays deleted.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool SeedInto(Configuration config) =>
        Seed(config.PhraseReplacements, config.SeededDefaultPhraseKeys, ignoreSeededKeys: false);

    /// <summary>
    /// Re-offer every shipped default, including ones already seeded before.
    /// Additive only: an entry the user still has is left exactly as it is.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool RestoreAll(Configuration config) =>
        Seed(config.PhraseReplacements, config.SeededDefaultPhraseKeys, ignoreSeededKeys: true);

    /// <summary>
    /// Takes the lists rather than the whole <see cref="Configuration"/> so this stays
    /// independent of Dalamud's <c>IPluginConfiguration</c> and can be exercised directly.
    /// </summary>
    /// <param name="ignoreSeededKeys">
    /// False for the startup pass, which must respect deletions. True for the user-invoked
    /// restore, whose entire purpose is to re-offer what was deleted.
    /// </param>
    public static bool Seed(
        List<PhraseReplacement> phrases,
        List<string> seededKeys,
        bool ignoreSeededKeys)
    {
        var alreadySeeded = new HashSet<string>(seededKeys, StringComparer.Ordinal);
        var changed = false;

        foreach (var (from, to) in DefaultPhraseMaps.Entries)
        {
            var isNewKey = !alreadySeeded.Contains(from);
            if (!isNewKey && !ignoreSeededKeys)
                continue;

            if (!phrases.Exists(p => string.Equals(p.From, from, StringComparison.Ordinal)))
            {
                phrases.Add(new PhraseReplacement { From = from, To = to, Enabled = true });
                changed = true;
            }

            if (isNewKey)
            {
                alreadySeeded.Add(from);
                seededKeys.Add(from);
                changed = true;
            }
        }

        return changed;
    }
}
```

實作要點：`changed` 只在**真的動了資料**時才為真。`RestoreAll` 遇到使用者還留著的條目時不加、不改、不記錄，因此回傳 `false` —— 這正是第四個測試要驗的。

- [ ] **Step 4: 跑測試確認通過**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet test --filter "FullyQualifiedName~DefaultPhraseSeederTests"
```

預期：`Passed! - Failed: 0, Passed: 4`。

- [ ] **Step 5: 跑全部測試確認沒有回歸**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet test
```

預期：`Passed: 136`（原 132 + 新 4）。

---

### Task 4: Configuration 新欄位

**Files:**
- Modify: `NoMoreEquals/Configuration.cs:17,44,61`

**Interfaces:**
- Consumes: 無
- Produces: `Configuration.SeededDefaultPhraseKeys`，型別 `List<string>`

- [ ] **Step 1: 加欄位**

在既有的 `SeededDefaultGlyphKeys`（第 44 行）之後插入：

```csharp
    /// <summary>
    /// Curated default phrase keys (the <c>From</c> side) already offered to this install.
    /// Mirrors <see cref="SeededDefaultGlyphKeys"/>: prevents re-adding a default the user
    /// deleted, while still allowing newly shipped defaults to be seeded on upgrade.
    /// </summary>
    public List<string> SeededDefaultPhraseKeys { get; set; } = [];
```

- [ ] **Step 2: 在 Normalize 補防呆**

第 61 行 `this.SeededDefaultGlyphKeys ??= [];` 之後加：

```csharp
        this.SeededDefaultPhraseKeys ??= [];
```

- [ ] **Step 3: 升 schema 版本**

第 17 行由 `public const int CurrentVersion = 3;` 改為 `= 4;`。

**不要**新增 migration 分支。依該欄位既有註解，目前的 schema 變更都只是「新增有預設值的欄位」，蓋版本號即足夠；`??=` 已處理舊檔沒有這個欄位的情況。

**不要**更動 `SeededDefaultGlyphKeys` 的名稱（見 Global Constraints）。

- [ ] **Step 4: 建置並測試**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet build && dotnet test
```

預期：建置成功，`Passed: 136`。

---

### Task 5: DefaultGlyphSeeder 重構加 RestoreAll（TDD）

把 glyph seeder 拆成與 phrase 相同的形狀，順帶讓它變成可測 —— 它現在完全沒有測試覆蓋。

**Files:**
- Modify: `NoMoreEquals/Services/DefaultGlyphSeeder.cs`
- Create: `tests/NoMoreEquals.Tests/DefaultGlyphSeederTests.cs`

**Interfaces:**
- Consumes: `DefaultGlyphMaps.Entries`（Task 1）
- Produces:
  - `DefaultGlyphSeeder.Seed(Dictionary<string, string> mappings, List<string> seededKeys, bool ignoreSeededKeys) -> bool`
  - `DefaultGlyphSeeder.SeedInto(Configuration config) -> bool`（簽章不變）
  - `DefaultGlyphSeeder.RestoreAll(Configuration config) -> bool`

注意 mapping 是 `Dictionary<string, string>`（key 為單字字串），與 phrase 的 `List<PhraseReplacement>` 不同。

- [ ] **Step 1: 寫失敗的測試**

建立 `tests/NoMoreEquals.Tests/DefaultGlyphSeederTests.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;
using NoMoreEquals.Data.Defaults;
using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

/// <summary>
/// Same Dalamud constraint as <see cref="DefaultPhraseSeederTests"/>: these call the
/// list-based core, never <see cref="Configuration"/>.
/// </summary>
public class DefaultGlyphSeederTests
{
    private static string FirstDefaultKey => DefaultGlyphMaps.Entries.Keys.First().ToString();

    private static string FirstDefaultValue => DefaultGlyphMaps.Entries.Values.First().ToString();

    [Fact]
    public void Seed_PopulatesEmptyConfigAndRecordsKeys()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();

        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        Assert.True(changed);
        Assert.Equal(DefaultGlyphMaps.Entries.Count, mappings.Count);
        Assert.Equal(FirstDefaultValue, mappings[FirstDefaultKey]);
        Assert.Contains(FirstDefaultKey, seeded);
    }

    [Fact]
    public void Seed_DoesNotResurrectADeletedDefault()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();
        DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        mappings.Remove(FirstDefaultKey);
        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        Assert.False(changed);
        Assert.False(mappings.ContainsKey(FirstDefaultKey));
    }

    [Fact]
    public void RestoreAll_AddsBackADeletedDefault()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();
        DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);
        mappings.Remove(FirstDefaultKey);

        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: true);

        Assert.True(changed);
        Assert.Equal(FirstDefaultValue, mappings[FirstDefaultKey]);
    }

    [Fact]
    public void RestoreAll_PreservesAUserEditedValue()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();
        DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        mappings[FirstDefaultKey] = "\u6E2C"; // 測

        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: true);

        Assert.False(changed);
        Assert.Equal("\u6E2C", mappings[FirstDefaultKey]);
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet test --filter "FullyQualifiedName~DefaultGlyphSeederTests"
```

預期：編譯失敗，`DefaultGlyphSeeder.Seed` 不存在（目前只有收 `Configuration` 的 `SeedInto`）。

- [ ] **Step 3: 重構實作**

`NoMoreEquals/Services/DefaultGlyphSeeder.cs` 整檔改為：

```csharp
using System;
using System.Collections.Generic;
using NoMoreEquals.Data.Defaults;

namespace NoMoreEquals.Services;

/// <summary>
/// Copies shipped glyph defaults into the user's editable mappings on first sight.
/// </summary>
internal static class DefaultGlyphSeeder
{
    /// <summary>
    /// Seed not-yet-offered defaults into <see cref="Configuration.CustomMappings"/>.
    /// Does not overwrite an existing custom entry. Marks each key as seeded so
    /// a user who deletes a default will not get it back on the next launch.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool SeedInto(Configuration config) =>
        Seed(config.CustomMappings, config.SeededDefaultGlyphKeys, ignoreSeededKeys: false);

    /// <summary>
    /// Re-offer every shipped default, including ones already seeded before.
    /// Additive only: an entry the user still has is left exactly as it is.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool RestoreAll(Configuration config) =>
        Seed(config.CustomMappings, config.SeededDefaultGlyphKeys, ignoreSeededKeys: true);

    /// <summary>
    /// Takes the collections rather than the whole <see cref="Configuration"/> so this stays
    /// independent of Dalamud's <c>IPluginConfiguration</c> and can be exercised directly.
    /// </summary>
    /// <param name="ignoreSeededKeys">
    /// False for the startup pass, which must respect deletions. True for the user-invoked
    /// restore, whose entire purpose is to re-offer what was deleted.
    /// </param>
    public static bool Seed(
        Dictionary<string, string> mappings,
        List<string> seededKeys,
        bool ignoreSeededKeys)
    {
        var alreadySeeded = new HashSet<string>(seededKeys, StringComparer.Ordinal);
        var changed = false;

        foreach (var (from, to) in DefaultGlyphMaps.Entries)
        {
            var key = from.ToString();
            var isNewKey = !alreadySeeded.Contains(key);
            if (!isNewKey && !ignoreSeededKeys)
                continue;

            if (!mappings.ContainsKey(key))
            {
                mappings[key] = to.ToString();
                changed = true;
            }

            if (isNewKey)
            {
                alreadySeeded.Add(key);
                seededKeys.Add(key);
                changed = true;
            }
        }

        return changed;
    }
}
```

行為與原本等價：原本用 `alreadySeeded.Add(key)` 的回傳值同時當「是否新 key」與「記錄」用，這裡拆成 `Contains` + 明確的 `Add`，才容納得下 `ignoreSeededKeys` 這條路徑。

- [ ] **Step 4: 跑測試確認通過**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet test
```

預期：`Passed: 140`（136 + 新 4）。

---

### Task 6: Plugin 啟動時種入 phrase

**Files:**
- Modify: `NoMoreEquals/Plugin.cs:39-40`

**Interfaces:**
- Consumes: `DefaultPhraseSeeder.SeedInto`（Task 3）、`DefaultGlyphSeeder.SeedInto`（Task 5）
- Produces: 無

- [ ] **Step 1: 兩個種入合併成一次存檔**

現況：

```csharp
        if (DefaultGlyphSeeder.SeedInto(this.Configuration))
            this.ConfigService.Save();
```

改為：

```csharp
        // Both run: | not || so the phrase pass is never short-circuited away.
        var seeded = DefaultGlyphSeeder.SeedInto(this.Configuration)
                     | DefaultPhraseSeeder.SeedInto(this.Configuration);
        if (seeded)
            this.ConfigService.Save();
```

**關鍵：用 `|` 不是 `||`。** `||` 會在左邊為真時短路，導致 phrase 種入根本不執行。這是本 Task 唯一容易寫錯的地方，註解就是為了擋住日後有人「順手改成 `||`」。

- [ ] **Step 2: 建置並測試**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet build && dotnet test
```

預期：建置成功，`Passed: 140`。

---

### Task 7: Advanced 分頁「加入預設條目」按鈕

**Files:**
- Modify: `NoMoreEquals/Localization/UiStrings.cs`
- Modify: `NoMoreEquals/Localization/LocEn.cs`
- Modify: `NoMoreEquals/Localization/LocTw.cs`
- Modify: `NoMoreEquals/Windows/Tabs/AdvancedTab.cs`

**Interfaces:**
- Consumes: `DefaultGlyphSeeder.RestoreAll`（Task 5）、`DefaultPhraseSeeder.RestoreAll`（Task 3）、`ConfigWindowContext.ApplyAndSave()`（既有）
- Produces: 無

- [ ] **Step 1: 加 UiStrings 欄位**

在 `RebuildFromFont`（第 59 行）之後加入三個 `required` 屬性：

```csharp
    public required string RestoreDefaults { get; init; }

    public required string RestoreDefaultsDone { get; init; }

    public required string RestoreDefaultsNothing { get; init; }
```

`UiStrings` 的屬性都是 `required`，所以漏加任何一個語系的值都會是**編譯錯誤**，不會靜默漏翻。

- [ ] **Step 2: 加英文字串**

`LocEn.cs` 的 `RebuildFromFont`（第 41 行）之後：

```csharp
        RestoreDefaults = "Add missing default entries",
        RestoreDefaultsDone = "Added {0} entr(y/ies).",
        RestoreDefaultsNothing = "Nothing to add — all defaults are already present.",
```

- [ ] **Step 3: 加中文字串**

`LocTw.cs` 的 `RebuildFromFont`（第 42 行）之後：

```csharp
        RestoreDefaults = "加入缺少的預設條目",
        RestoreDefaultsDone = "已加入 {0} 筆。",
        RestoreDefaultsNothing = "沒有可加入的項目，預設條目都在。",
```

用「加入缺少的預設條目」而非「還原預設」：這顆按鈕不會把使用者改過的條目還原成預設值，只補缺少的。用「還原」會讓使用者期待一個它不執行的重置。

- [ ] **Step 4: 加按鈕**

`AdvancedTab.cs` 需要一個欄位存放結果訊息（訊息要跨影格留存，不能只是區域變數）。在 `private readonly ConfigWindowContext context;`（第 20 行）之後加：

```csharp
    /// <summary>Result of the last restore click, or empty. Cleared on any mouse press.</summary>
    private string restoreMessage = string.Empty;
```

然後在 `RebuildFromFont` 按鈕（第 42-43 行）之後插入：

```csharp
        // Same dismissal rule as the Main tab's errors: any click clears the last result.
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            this.restoreMessage = string.Empty;

        if (ImGui.Button(t.RestoreDefaults))
        {
            var added = DefaultGlyphSeeder.RestoreAll(config)
                        | DefaultPhraseSeeder.RestoreAll(config);

            this.restoreMessage = added
                ? string.Format(t.RestoreDefaultsDone, this.CountRestored())
                : t.RestoreDefaultsNothing;

            if (added)
                this.context.ApplyAndSave();
        }

        if (this.restoreMessage.Length > 0)
            UiHelpers.StatusText(this.restoreMessage);
```

問題：`RestoreAll` 只回傳 `bool`，拿不到筆數。**最簡解是改回傳筆數**——但那會動到 Task 3／5 已通過的測試。

改用這個作法：**在呼叫前記錄兩個集合的大小，呼叫後相減。**

把上面那段改成：

```csharp
        if (ImGui.Button(t.RestoreDefaults))
        {
            var before = config.CustomMappings.Count + config.PhraseReplacements.Count;

            // | not ||: both passes must run.
            var added = DefaultGlyphSeeder.RestoreAll(config)
                        | DefaultPhraseSeeder.RestoreAll(config);

            var delta = config.CustomMappings.Count + config.PhraseReplacements.Count - before;

            this.restoreMessage = delta > 0
                ? string.Format(t.RestoreDefaultsDone, delta)
                : t.RestoreDefaultsNothing;

            if (added)
                this.context.ApplyAndSave();
        }
```

用 `delta > 0` 而非 `added` 決定訊息：`added` 有可能因為「只補記了 seeded key、沒真的加條目」而為真，那種情況對使用者而言等於什麼都沒發生。

`AdvancedTab.cs` 已經 `using NoMoreEquals.Services;`（第 6 行），兩個 seeder 都在該 namespace 下，不需要新的 `using`。

- [ ] **Step 5: 建置並測試**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet build && dotnet test
```

預期：建置成功，`Passed: 140`。

- [ ] **Step 6: 確認沒有殘留的舊名稱**

```bash
cd /d/Work/Coding/project/NoMoreEquals
grep -rn "DefaultMissingGlyph" --include=*.cs .
git status --short
```

預期：`grep` 無輸出；`git status` 顯示預期中的新增與修改，且**沒有 commit**（依 Global Constraints，提交由維護者自行決定）。

---

### Task 8: 遊戲內驗證

單元測試碰不到 Dalamud，所以種入實際寫進 `NoMoreEquals.json` 這條路只能在遊戲裡驗。

**Files:** 無（純手動驗證）

- [ ] **Step 1: 建置 Release 並安裝**

```bash
cd /d/Work/Coding/project/NoMoreEquals
dotnet build -c Release
```

依專案既有方式載入 Dalamud（dev plugin 路徑指向 `NoMoreEquals/bin/x64/Release/`）。

- [ ] **Step 2: 驗證首次種入**

開啟設定視窗，Main 分頁的片語清單應出現 **咖啡 → 珈琲**。

- [ ] **Step 3: 驗證實際轉換**

聊天欄打「咖啡」，確認送出前預覽顯示「珈琲」。

- [ ] **Step 4: 驗證刪除後不復活**

刪掉 咖啡 → 珈琲，重開遊戲（或重載外掛），確認它**沒有**回來。

- [ ] **Step 5: 驗證按鈕補回**

Advanced 分頁按「加入缺少的預設條目」，確認：
- 顯示「已加入 1 筆。」
- 回 Main 分頁，咖啡 → 珈琲 回來了

- [ ] **Step 6: 驗證只增不改**

把 咖啡 的目標改成別的字（例如 咖啡廳），再按一次按鈕。確認：
- 顯示「沒有可加入的項目，預設條目都在。」
- 使用者改的值**沒有被蓋掉**

- [ ] **Step 7: 驗證舊 config 升級**

若有 v0.2.0 時期的 `NoMoreEquals.json` 備份，用它啟動一次，確認：
- 不會因為缺少 `SeededDefaultPhraseKeys` 而崩潰
- 既有的 glyph 設定完好
- 咖啡 → 珈琲 被種入

---

## Self-Review

**Spec 覆蓋檢查**

| Spec 要求 | 對應 Task |
|---|---|
| 純 C# data class，不用 JSON／產生器 | Task 2 |
| 資料夾與 namespace 改名為 `Data/Defaults` | Task 1 |
| `DefaultMissingGlyphMaps` → `DefaultGlyphMaps` | Task 1 |
| `CommonPhrases` 初始含 咖啡 → 珈琲 | Task 2 |
| `SeededDefaultPhraseKeys` 欄位、`Normalize` 防呆、版本 3→4 | Task 4 |
| `SeededDefaultGlyphKeys` 不改名 | Global Constraints + Task 4 Step 3 |
| 兩條種入路徑（自動查收據／按鈕忽略） | Task 3、Task 5 |
| 只增不改語意 | Task 3 Step 3、Task 5 Step 3；測試於各 Task Step 1 |
| seeder 核心不收 `Configuration` | Task 3、Task 5 |
| Plugin 啟動時種入 | Task 6 |
| Advanced 按鈕、三個語系字串 | Task 7 |
| 按鈕文字用「加入」非「還原」 | Task 7 Step 3 |
| 四個測試案例 | Task 3 Step 1（phrase）、Task 5 Step 1（glyph） |
| Unicode escape 字面 | Global Constraints；Task 2、3、5、7 的程式碼皆遵循 |

無遺漏。

**型別一致性檢查**

- `DefaultPhraseSeeder.Seed(List<PhraseReplacement>, List<string>, bool)` —— Task 3 定義，Task 3 測試與 Task 7 間接使用，一致
- `DefaultGlyphSeeder.Seed(Dictionary<string, string>, List<string>, bool)` —— Task 5 定義；型別與 phrase 版不同（`Dictionary` vs `List`），已於 Task 5 Interfaces 明示
- `DefaultGlyphMaps.Entries` 為 `IReadOnlyDictionary<char, char>`，故測試中 key 需 `.ToString()` —— Task 5 測試已處理
- `DefaultPhraseMaps.Entries` 為 `IReadOnlyDictionary<string, string>`，key 直接可用 —— Task 3 測試已處理
- `RestoreAll(Configuration)` 於 Task 3／5 定義，Task 7 使用，簽章一致
- `UiHelpers.StatusText` 為既有 API（`MainTab` 第 123 行已用）

**已知風險**

無未解風險。`InternalsVisibleTo` 已確認存在於 `NoMoreEquals.csproj:16`；`Configuration` 的 Dalamud 相依已於設計階段實證並反映在 seeder 的參數形狀上。
