# NoMoreEquals

將遊戲內聊天的缺失中文字符替換為日文漢字的 Dalamud 插件，目前僅支援繁體中文轉換。

Dalamud plugin that replaces Chinese characters missing from FFXIV’s AXIS UI font with Japanese kanji **while you type in chat**.

Examples (when AXIS lacks the Chinese form):

| Input | Output |
| --- | --- |
| 綠 | 緑 |
| 黑 | 黒 |
| 每 | 毎 |

Characters that already exist in AXIS (e.g. 圖, 邊, 國, 學) are **not** converted.

## How mappings are chosen

1. Start from OpenCC `JPShinjitai` candidates (Chinese/kyujitai → Japanese).
2. Keep a pair only if:
   - AXIS does **not** contain the Chinese form, and
   - AXIS **does** contain the Japanese form.
3. At runtime the plugin re-reads `common/font/AXIS_12.fdt` so the active set matches your installed client (and patches).
4. You can add custom overrides in the config UI.

Offline regeneration (optional, for updating the shipped fallback map after game patches):

```powershell
# Needs OpenCC checked out at _tmp_opencc (gitignored) only when regenerating candidates.
python tools/generate_candidates.py

# Auto-detects FFXIV via XIVLauncher config / common install paths / Steam libraries.
# Or pass the game root explicitly as argv[0].
dotnet run --project tools/FontProbe/FontProbe
```

## Build

Requires [.NET SDK](https://dotnet.microsoft.com/download) and a local Dalamud install (XIVLauncher).

```powershell
$env:DALAMUD_HOME = "$env:APPDATA\XIVLauncher\addon\Hooks\dev"
dotnet build NoMoreEquals.sln -c Debug
```

Output DLL: `NoMoreEquals/bin/x64/Debug/NoMoreEquals/NoMoreEquals.dll` (path may vary with SDK).

## Install for testing

1. In XIVLauncher → Settings → Dalamud → enable **Dev Plugins** / custom plugin path.
2. Point DevPlugins at the build output folder that contains `NoMoreEquals.dll` + `NoMoreEquals.json`.
3. Launch the game and load the plugin.

## Localization

only 'en' and 'tw' are surrpoted.

## Usage

- `/nme` or `/nomoreequals` — open settings
- **Main** tab: preview, phrase replacements, glyph overrides
- **Advanced** tab: AXIS font filter / rebuild (optional)

Chat lines starting with `/` are skipped by default so slash commands are not rewritten.

### AXIS / Advanced

AXIS is FFXIV’s main UI/chat font. On load the plugin reads `AXIS_12.fdt` and keeps only Chinese→Japanese pairs that are actually missing. Rebuild controls live on the **Advanced** tab.

## License

AGPL-3.0-or-later (same family as Dalamud SamplePlugin).

OpenCC dictionary data used for candidates is Apache-2.0 (BYVoid/OpenCC).
