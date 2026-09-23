using DeskPet.Core.Brain;

namespace DeskPet.Core.Tests.Brain;

public sealed class TitlePatternTests
{
    [Fact]
    public void UnreadCountersShareAKey() =>
        Assert.Equal(
            TitlePattern.Create("OUTLOOK", "Inbox (3) - user@x.com - Outlook"),
            TitlePattern.Create("OUTLOOK", "Inbox (5) - user@x.com - Outlook"));

    [Fact]
    public void SquareBracketCountersAndPrefixCountersAreRemoved() =>
        Assert.Equal(
            TitlePattern.Create("chrome", "(12) Home / X - Google Chrome"),
            TitlePattern.Create("chrome", "Home / X [99+] - Google Chrome"));

    [Fact]
    public void DigitsCollapse() =>
        Assert.Equal(
            TitlePattern.Create("Code", "report2024.md - Visual Studio Code"),
            TitlePattern.Create("Code", "report1.md - Visual Studio Code"));

    [Fact]
    public void WhitespaceAndCaseCollapse() =>
        Assert.Equal(
            TitlePattern.Create("Code", "  Main.cs   -\tVisual Studio Code "),
            TitlePattern.Create("code", "main.cs - visual studio code"));

    [Fact]
    public void ProcessIsLowerCasedAndExeStripped() =>
        Assert.StartsWith("chrome|", TitlePattern.Create("Chrome.EXE", "Tab"));

    [Fact]
    public void DifferentProcessesDiffer() =>
        Assert.NotEqual(TitlePattern.Create("chrome", "Docs"), TitlePattern.Create("firefox", "Docs"));

    [Fact]
    public void DifferentVideosMayDiffer() =>
        Assert.NotEqual(
            TitlePattern.Create("chrome", "Cat video - YouTube - Google Chrome"),
            TitlePattern.Create("chrome", "Rust talk - YouTube - Google Chrome"));

    [Fact]
    public void LongTitlesAreTruncatedTo80Chars()
    {
        var key = TitlePattern.Create("p", new string('a', 300));

        Assert.Equal("p|" + new string('a', TitlePattern.MaxTitleLength), key);
    }

    [Fact]
    public void TruncationKeepsTheAppSuffix()
    {
        var key = TitlePattern.Create("chrome", new string('a', 200) + " - YouTube");
        var title = key["chrome|".Length..];

        Assert.Equal(TitlePattern.MaxTitleLength, title.Length);
        Assert.EndsWith(" - youtube", title);
    }

    [Fact]
    public void OverlongSuffixIsJustTruncated()
    {
        var key = TitlePattern.Create("p", "head - " + new string('b', 200));

        Assert.Equal(TitlePattern.MaxTitleLength, key.Length - "p|".Length);
        Assert.StartsWith("p|head - ", key);
    }
}
