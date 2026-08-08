# NoMoreEquals

將遊戲內**聊天打字**中的缺失中文字符替換為日文漢字異體字的 Dalamud 插件，目前僅支援繁體中文轉換。

讓全世界沒有裝中文包的人也能看到你在寫什麼。

Dalamud plugin that replaces Chinese characters missing from FFXIV’s AXIS UI font with Japanese kanji **while you type in chat**.

Examples (when AXIS lacks the Chinese form):

| Input | Output |
| --- | --- |
| 綠 | 緑 |
| 黑 | 黒 |
| 每 | 毎 |

Characters are non-common Kanji but frequently used in Traditional Chinese, which already exist in AXIS (e.g. 圖, 邊, 國, 學) are **not** converted.

非日文常用漢字，但是有內建在遊戲內字體(AXIS)的常用繁體中文字(e.g. 圖, 邊, 國, 學)依舊**不會**被轉換。

## Why glyph mappings 為什麼要字符轉換

FFXIV font support Japanese, but not really every Chinese character can shows in game.

Which is why you see = (equals) in game when input Chinese character.

遊戲會缺字是因為字體包(AXIS)只有日文漢字沒有中文字，所以缺字那就會看見 =

![AXIS 缺字顯示為等號](docs/images/exampleimg01.png)

With the conversion list comes from comparing Chinese characters against FFXIV’s AXIS UI font (`AXIS_12.fdt`), this will map your chat input to a Japanese Kanji that AXIS does have.

經過比對字體包(`AXIS_12.fdt`)，篩選出字體包沒有的中文字，並在聊天框進行異體字轉換。

![根據 AXIS 缺字轉換後](docs/images/exampleimg02.png)

But sadly, "你"(you) is not a common character in Japanese Kanji, also no other replacement.

So you can use another customization to decide what to replace.

然而，「你」這個字並沒有對應的日文漢字。所以可以自定義成你想要的樣子。

## Install for testing

It's still under developement, only have the source code pack.現在還在開發中，只有原始碼包。

Free to clone and test whatever you want. 歡迎下載自己拿去玩。

I'll catch up coming update asap. 我會盡可能快點更新接下來的內容。

1. In XIVLauncher → Settings → Dalamud → enable **Dev Plugins** / custom plugin path.
2. Point DevPlugins at the build output folder that contains `NoMoreEquals.dll` + `NoMoreEquals.json`.
3. Launch the game and load the plugin.

## Localization

Only 'tw' localization currently supported.

中文介面目前只有繁中('tw')設定才有，其他的都是英文。

## License

AGPL-3.0-or-later (same family as Dalamud SamplePlugin).

OpenCC dictionary data used for candidates is Apache-2.0 (BYVoid/OpenCC).
