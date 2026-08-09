using NoMoreEquals.Data;
using Xunit;

namespace NoMoreEquals.Tests;

public class ChatChannelCommandsTests
{
    [Theory]
    // Long forms.
    [InlineData("say")]
    [InlineData("yell")]
    [InlineData("shout")]
    [InlineData("tell")]
    [InlineData("reply")]
    [InlineData("party")]
    [InlineData("alliance")]
    [InlineData("freecompany")]
    [InlineData("pvpteam")]
    [InlineData("novice")]
    [InlineData("beginner")]
    [InlineData("echo")]
    [InlineData("emote")]
    // Short forms.
    [InlineData("s")]
    [InlineData("y")]
    [InlineData("sh")]
    [InlineData("t")]
    [InlineData("r")]
    [InlineData("p")]
    [InlineData("a")]
    [InlineData("fc")]
    [InlineData("pt")]
    [InlineData("n")]
    [InlineData("b")]
    [InlineData("e")]
    [InlineData("em")]
    // Unnumbered linkshell channels (the currently active one).
    [InlineData("linkshell")]
    [InlineData("l")]
    [InlineData("cwlinkshell")]
    [InlineData("cwl")]
    public void IsChatChannel_AcceptsTheUnnumberedChannelTokens(string token)
    {
        Assert.True(ChatChannelCommands.IsChatChannel(token));
    }

    /// <summary>
    /// Pins the size, because the two directions are not equally dangerous. A dropped
    /// token already fails the tests around this one; an added token widens what the
    /// plugin rewrites and would otherwise land in silence. Changing this number means
    /// the design doc's table changes with it — and that an entry got a source.
    /// </summary>
    [Fact]
    public void Count_MatchesTheDocumentedWhitelistSize()
    {
        Assert.Equal(62, ChatChannelCommands.Count);
    }

    /// <summary>
    /// The numbered families are the easiest place to drop an entry while typing out
    /// the table, and a missing /l7 would only ever show up as "the plugin randomly
    /// stops working in one linkshell".
    /// </summary>
    [Fact]
    public void IsChatChannel_CoversTheNumberedLinkshellFamiliesInFull()
    {
        for (var i = 1; i <= 8; i++)
        {
            Assert.True(ChatChannelCommands.IsChatChannel($"linkshell{i}"));
            Assert.True(ChatChannelCommands.IsChatChannel($"l{i}"));
            Assert.True(ChatChannelCommands.IsChatChannel($"cwlinkshell{i}"));
            Assert.True(ChatChannelCommands.IsChatChannel($"cwl{i}"));
        }
    }

    [Theory]
    [InlineData("P")]
    [InlineData("Say")]
    [InlineData("CWL3")]
    [InlineData("FreeCompany")]
    public void IsChatChannel_IgnoresCase(string token)
    {
        Assert.True(ChatChannelCommands.IsChatChannel(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("gearset")]
    [InlineData("target")]
    [InlineData("nme")]           // this plugin's own command
    [InlineData("quickchat")]     // argument is a preset id, not free text
    [InlineData("qchat")]
    [InlineData("ls1")]
    [InlineData("cwls")]          // circulates among players but the game rejects it
    [InlineData("cwls1")]
    [InlineData("linkshell9")]    // there are only ever eight of each
    [InlineData("cwlinkshell9")]
    [InlineData("l9")]
    [InlineData("cwl9")]
    [InlineData("/p")]            // a second slash is not a chat command
    public void IsChatChannel_RejectsEverythingElse(string token)
    {
        Assert.False(ChatChannelCommands.IsChatChannel(token));
    }
}
