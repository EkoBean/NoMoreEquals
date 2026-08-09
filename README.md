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

## Slash commands 斜線指令

Conversion only runs on lines that go to a chat channel. `/say`, `/p`, `/tell`, `/fc`, `/b`, `/l1`–`/l8`, `/cwls1`–`/cwls8`, `/em` and the rest of the chat commands have their message converted, with the command itself left untouched. Everything else — `/gearset`, `/target`, other plugins' commands — is left completely alone: no conversion, no preview. Their arguments are identifiers, not messages, and substituting a glyph there would break the command.

While you are still typing the command itself (no space yet), nothing happens either — the line could still turn out to be either kind.

只有會送進聊天頻道的行才會轉換。`/say`、`/p`、`/tell`、`/fc`、`/b`、`/l1`–`/l8`、`/cwls1`–`/cwls8`、`/em` 等聊天指令會轉換訊息本文，指令詞本身不動。其他指令（`/gearset`、`/target`、其他插件的指令）完全不介入，不轉換也不顯示預覽 —— 那些參數是識別碼不是訊息，換掉字形會讓指令失效。

指令詞還沒打完（還沒有空格）時同樣不作用，因為那時還看不出來是哪一種。

The full list lives in [`ChatChannelCommands.cs`](NoMoreEquals/Data/ChatChannelCommands.cs). 完整清單在該檔案裡。

## Install

This plugin is not on the official Dalamud repository yet, so add this custom repo:

還沒上官方倉庫，請先加入自訂倉庫：

```
https://raw.githubusercontent.com/EkoBean/NoMoreEquals/master/repo.json
```

1. In game, type `/xlsettings` to open Dalamud settings. 遊戲內輸入 `/xlsettings` 開啟 Dalamud 設定。
2. Go to **Experimental** → **Custom Plugin Repositories**. 切到「實驗性」分頁，找到自訂插件倉庫。
3. Paste the URL above, press **+**, then **Save and Close**. 貼上網址，按 **+**，再按儲存並關閉。
4. Open `/xlplugins`, search for **NoMoreEquals**, and install. 打開插件安裝器搜尋 NoMoreEquals 安裝。

## Localization

Only 'tw' localization currently supported.

中文介面目前只有繁中('tw')設定才有，其他的都是英文。
