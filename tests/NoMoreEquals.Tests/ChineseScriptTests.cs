using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

public class ChineseScriptTests
{
    [Theory]
    [InlineData("綠色")]
    [InlineData("綠，")]        // one Han character is enough; the comma does not disqualify it
    [InlineData("a好")]
    [InlineData("ㄅㄅ")]
    public void HasChineseCharacter_AcceptsTextContainingChinese(string text)
    {
        Assert.True(ChineseScript.HasChineseCharacter(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("!!!")]
    [InlineData("   ")]
    [InlineData("あいう")]      // kana is Japanese, not Chinese
    [InlineData("アイウ")]
    public void HasChineseCharacter_RejectsTextWithoutAny(string? text)
    {
        Assert.False(ChineseScript.HasChineseCharacter(text));
    }

    /// <summary>
    /// One representative from each Han block. The Extension B case is the one that
    /// matters: it is a surrogate pair, so a scan that walks chars instead of runes sees
    /// two values in the D800 range and answers false.
    /// </summary>
    [Theory]
    [InlineData("㐀")]      // Extension A
    [InlineData("一")]      // Unified, 一
    [InlineData("豈")]      // Compatibility
    [InlineData("\U00020000")]  // Extension B
    public void HasChineseCharacter_CoversEveryHanBlock(string text)
    {
        Assert.True(ChineseScript.HasChineseCharacter(text));
    }

    /// <summary>
    /// Bopomofo counts. ㄅㄅ and ㄏㄏ are what Taiwanese players actually type, and those
    /// symbols are exactly the ones AXIS renders as '=' — this plugin's own job.
    /// </summary>
    [Theory]
    [InlineData("ㄅ")]      // ㄅ
    [InlineData("ㄯ")]      // last of the Bopomofo block
    [InlineData("ㆠ")]      // Bopomofo Extended
    [InlineData("ˊ")]      // ˊ, a tone mark
    public void HasChineseCharacter_AcceptsBopomofo(string text)
    {
        Assert.True(ChineseScript.HasChineseCharacter(text));
    }
}
