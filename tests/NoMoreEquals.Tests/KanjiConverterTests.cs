using System.Collections.Generic;
using NoMoreEquals;
using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

public class KanjiConverterTests
{
    // 綠 -> 緑, 黑 -> 黒: two of the shipped defaults.
    private static readonly Dictionary<char, char> GlyphMap = new()
    {
        ['綠'] = '緑',
        ['黑'] = '黒',
    };

    private const string Green = "綠";      // 綠
    private const string GreenJp = "緑";    // 緑
    private const string Black = "黑";      // 黑
    private const string BlackJp = "黒";    // 黒

    [Fact]
    public void ConvertAll_MapsKnownGlyphs()
    {
        Assert.Equal(GreenJp + BlackJp, Convert(Green + Black));
    }

    [Fact]
    public void ConvertAll_ReturnsSameInstanceWhenNothingChanges()
    {
        const string input = "no mapped glyphs here";
        Assert.Same(input, Convert(input));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("hello", 0)]
    [InlineData("half/way", 0)]
    [InlineData("/", 1)]
    [InlineData("/p", 2)]
    [InlineData("/p ", 3)]
    [InlineData("/p hello", 3)]
    [InlineData("/tell Name@World hi", 6)]
    public void GetChatBodyStart_FindsMessageBody(string input, int expected)
    {
        Assert.Equal(expected, KanjiConverter.GetChatBodyStart(input));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("hello", false)]
    [InlineData("/p", true)]
    [InlineData("/tell", true)]
    [InlineData("/p ", false)]
    [InlineData("/p hello", false)]
    public void IsInsideCommandToken_OnlyBeforeTheFirstSpace(string input, bool expected)
    {
        Assert.Equal(expected, KanjiConverter.IsInsideCommandToken(input));
    }

    /// <summary>
    /// The regression this exists for: both conversion layers used to bail out on any
    /// leading '/', which silently disabled the plugin for every channel command.
    /// </summary>
    [Fact]
    public void ConvertChatLine_ConvertsCommandArgumentsButNotTheCommand()
    {
        Assert.Equal("/p " + GreenJp, ConvertLine("/p " + Green));
        Assert.Equal("/tell Name@World " + GreenJp, ConvertLine("/tell Name@World " + Green));
    }

    [Fact]
    public void ConvertChatLine_LeavesABareCommandTokenAlone()
    {
        // No space yet, so every character still belongs to the command itself.
        Assert.Equal("/" + Green, ConvertLine("/" + Green));
        Assert.Equal("/p", ConvertLine("/p"));
        Assert.Equal("/p ", ConvertLine("/p "));
    }

    [Fact]
    public void ConvertChatLine_ConvertsAnOrdinaryLineWhole()
    {
        Assert.Equal(GreenJp + BlackJp, ConvertLine(Green + Black));
    }

    [Fact]
    public void ConvertChatLine_AppliesPhrasesBeforeGlyphs()
    {
        var phrases = Phrases(new PhraseReplacement { From = "green", To = Green });

        // "green" becomes 綠 via the phrase rule, which the glyph map then maps to 緑.
        Assert.Equal("/p " + GreenJp, KanjiConverter.ConvertChatLine("/p green", phrases, GlyphMap));
    }

    private static string Convert(string input)
        => KanjiConverter.ConvertAll(input, Phrases(), GlyphMap);

    private static string ConvertLine(string input)
        => KanjiConverter.ConvertChatLine(input, Phrases(), GlyphMap);

    private static PhraseReplacementService Phrases(params PhraseReplacement[] rules)
    {
        var service = new PhraseReplacementService();
        service.Rebuild(rules);
        return service;
    }
}
