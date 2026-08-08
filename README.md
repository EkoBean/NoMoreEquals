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

## Glyph mappings

The conversion list comes from comparing Chinese characters against FFXIV’s AXIS UI font (`AXIS_12.fdt`): if AXIS is missing a glyph, we map it to a Japanese form that AXIS does have. You can add custom overrides in the config UI.

## Install for testing

1. In XIVLauncher → Settings → Dalamud → enable **Dev Plugins** / custom plugin path.
2. Point DevPlugins at the build output folder that contains `NoMoreEquals.dll` + `NoMoreEquals.json`.
3. Launch the game and load the plugin.

## Localization

Only 'tw' localization currently supported.

## License

AGPL-3.0-or-later (same family as Dalamud SamplePlugin).

OpenCC dictionary data used for candidates is Apache-2.0 (BYVoid/OpenCC).
