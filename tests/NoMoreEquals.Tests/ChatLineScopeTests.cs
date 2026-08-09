using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

public class ChatLineScopeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("綠")]
    [InlineData("half/way through a line")]
    public void Of_OrdinaryLine_ConvertsFromTheStart(string? line)
    {
        Assert.Equal(new ChatLineScope(true, 0), ChatLineScope.Of(line));
    }

    /// <summary>
    /// Nothing may happen while the command token itself is still being typed: at this
    /// point the line could still turn into either a chat message or a /gearset call.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/p")]
    [InlineData("/tell")]
    [InlineData("/gearset")]
    public void Of_CommandTokenStillBeingTyped_IsNotConvertible(string line)
    {
        Assert.False(ChatLineScope.Of(line).Convertible);
    }

    /// <summary>
    /// -1, not 0: a blocked line's <c>BodyStart</c> must not be a usable index. 0 means
    /// "convert from the start", so a caller that read it without checking
    /// <c>Convertible</c> would convert the whole /gearset line — the exact half-an-answer
    /// bug this type exists to make unrepresentable.
    /// </summary>
    [Theory]
    [InlineData("/p")]
    [InlineData("/gearset change 1")]
    public void Of_BlockedLine_HasNoUsableBodyStart(string line)
    {
        Assert.Equal(new ChatLineScope(false, -1), ChatLineScope.Of(line));
    }

    [Theory]
    [InlineData("/p ", 3)]
    [InlineData("/p 綠", 3)]
    [InlineData("/p  綠", 3)]              // the extra space belongs to the body
    [InlineData("/P 綠", 3)]               // the game accepts either case
    [InlineData("/cwl3 綠", 6)]
    [InlineData("/tell Name@World 綠", 6)]
    [InlineData("/p ===", 3)]              // '=' noise in RawString does not confuse the prefix
    public void Of_ChatChannelCommand_ConvertsTheBodyOnly(string line, int expectedBodyStart)
    {
        Assert.Equal(new ChatLineScope(true, expectedBodyStart), ChatLineScope.Of(line));
    }

    [Theory]
    [InlineData("/gearset change 1")]
    [InlineData("/target 綠")]
    [InlineData("/nme")]                   // this plugin's own command
    [InlineData("//p 綠")]                 // token is "/p", which is not a command
    [InlineData("/ 綠")]                   // empty token
    public void Of_NonChatCommand_IsNotConvertible(string line)
    {
        Assert.False(ChatLineScope.Of(line).Convertible);
    }

    /// <summary>
    /// Only a halfwidth space separates the command from its body. A CJK IME will happily
    /// produce U+3000 there, but the game cannot parse that line either — a command it
    /// rejects is one this plugin has no reason to act on.
    /// </summary>
    [Fact]
    public void Of_FullWidthSpaceDoesNotEndTheCommandToken()
    {
        Assert.False(ChatLineScope.Of("/p　綠").Convertible);
    }
}
