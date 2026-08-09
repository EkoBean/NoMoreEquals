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
    [InlineData("e")]
    [InlineData("em")]
    // Unnumbered linkshell channels (the currently active one).
    [InlineData("linkshell")]
    [InlineData("l")]
    [InlineData("cwlinkshell")]
    [InlineData("cwl")]
    public void IsChatChannel_AcceptsEveryChatChannelToken(string token)
    {
        Assert.True(ChatChannelCommands.IsChatChannel(token));
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
    [InlineData("beginner")]      // not a documented alias for the Novice Network
    [InlineData("b")]
    [InlineData("cwls1")]         // not a documented alias for /cwlinkshell1
    [InlineData("ls1")]
    [InlineData("/p")]            // a second slash is not a chat command
    public void IsChatChannel_RejectsEverythingElse(string token)
    {
        Assert.False(ChatChannelCommands.IsChatChannel(token));
    }
}
