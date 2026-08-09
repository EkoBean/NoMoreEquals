using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

public class ChatBufferHeuristicsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("==", false)]          // under the minimum length
    [InlineData("===", true)]
    [InlineData("hello", false)]
    [InlineData("a=b=c=d", false)]     // 3 of 7
    public void IsMostlyEqualsNoise_FlagsDenseRunsOfEquals(string? input, bool expected)
    {
        Assert.Equal(expected, ChatBufferHeuristics.IsMostlyEqualsNoise(input));
    }

    /// <summary>
    /// Documents a known false positive. It is why callers may only consult this while
    /// an IME composition is in progress — after composition ends the buffer is read
    /// directly, so a line like "a==b" is converted and previewed normally.
    /// </summary>
    [Fact]
    public void IsMostlyEqualsNoise_MisreadsShortTextWithEquals()
    {
        Assert.True(ChatBufferHeuristics.IsMostlyEqualsNoise("a==b"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("abc", "")]
    [InlineData("abcㄅㄨˋ", "ㄅㄨˋ")]  // trailing ㄅㄨˋ
    [InlineData("ㄅㄨ", "ㄅㄨ")]
    [InlineData("ㄅabc", "")]                                 // Bopomofo, but not trailing
    public void TrailingBopomofo_TakesOnlyTheTrailingRun(string input, string expected)
    {
        Assert.Equal(expected, ChatBufferHeuristics.TrailingBopomofo(input));
    }

    [Fact]
    public void TrailingBopomofo_RejectsEqualsNoise()
    {
        // The game's '=' substitution must never be mistaken for a live reading.
        Assert.Equal(string.Empty, ChatBufferHeuristics.TrailingBopomofo("===ㄅ"));
    }

    [Theory]
    [InlineData('ㄅ', true)]   // ㄅ
    [InlineData('ㄯ', true)]   // ㄯ
    [InlineData('ˋ', true)]   // ˋ
    [InlineData('a', false)]
    [InlineData('=', false)]
    [InlineData('綠', false)]  // 綠
    public void IsBopomofo_CoversTheReadingBlocks(char c, bool expected)
    {
        Assert.Equal(expected, ChatBufferHeuristics.IsBopomofo(c));
    }
}
