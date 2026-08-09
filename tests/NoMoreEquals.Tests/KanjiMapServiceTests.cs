using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

public class KanjiMapServiceTests
{
    [Theory]
    [InlineData("綠", '綠')]  // 綠
    [InlineData("a", 'a')]
    public void TryParseSingleChar_AcceptsOneBmpRune(string input, char expected)
    {
        Assert.True(KanjiMapService.TryParseSingleChar(input, out var c));
        Assert.Equal(expected, c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("綠黑")]      // two glyphs
    [InlineData("𠀀")]      // outside the BMP: cannot fit a char map entry
    public void TryParseSingleChar_RejectsEverythingElse(string? input)
    {
        Assert.False(KanjiMapService.TryParseSingleChar(input, out var c));
        Assert.Equal(default, c);
    }
}
