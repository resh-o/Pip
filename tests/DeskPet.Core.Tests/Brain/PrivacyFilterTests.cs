using DeskPet.Core.Brain;
using DeskPet.Core.Config;

namespace DeskPet.Core.Tests.Brain;

public sealed class PrivacyFilterTests
{
    private static readonly Settings Defaults = new();

    [Theory]
    [InlineData("chrome", "Rust book - Google Chrome")]
    [InlineData("Code", "main.rs - Visual Studio Code")]
    public void OrdinaryWindowsAreSendable(string process, string title) =>
        Assert.True(PrivacyFilter.IsSendable(Defaults, process, title));

    [Theory]
    [InlineData("1Password")]
    [InlineData("1password")]
    [InlineData("KEEPASSXC")]
    [InlineData("Bitwarden.exe")]
    public void IgnoredProcessesAreWithheldCaseInsensitively(string process) =>
        Assert.False(PrivacyFilter.IsSendable(Defaults, process, "Anything"));

    [Theory]
    [InlineData("MyBank - Online Banking - Google Chrome")]
    [InlineData("Change PASSWORD")]
    [InlineData("New InPrivate tab - Microsoft Edge")]
    [InlineData("new incognito tab")]
    [InlineData("Mozilla Firefox Private Browsing")]
    public void IgnoredTitleKeywordsAreWithheldCaseInsensitively(string title) =>
        Assert.False(PrivacyFilter.IsSendable(Defaults, "chrome", title));

    [Fact]
    public void BlankKeywordsDoNotWithholdEverything()
    {
        var settings = new Settings { IgnoreTitleKeywords = ["", "  "], IgnoreProcesses = [] };

        Assert.True(PrivacyFilter.IsSendable(settings, "chrome", "Docs"));
    }

    [Fact]
    public void WindowOverloadUsesProcessAndTitle() =>
        Assert.False(PrivacyFilter.IsSendable(Defaults, BrainTestData.Window("chrome", "Vault login")));
}
