using DeskPet.Core.Config;
using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Planning;

public sealed class SafetyPolicyTests
{
    private static SafetyPolicy Policy(Settings? settings = null) => new(settings ?? new Settings(), "DeskPet");

    [Fact]
    public void Ordinary_window_is_touchable()
    {
        Assert.True(Policy().CanTouch(Windows.Make(1, "chrome", "YouTube - Google Chrome", "Chrome_WidgetWin_1")));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("#32770")]
    [InlineData("shell_traywnd")]
    public void Shell_and_dialog_classes_are_untouchable(string className)
    {
        Assert.False(Policy().CanTouch(Windows.Make(1, className: className)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Untitled_windows_are_untouchable(string title)
    {
        Assert.False(Policy().CanTouch(Windows.Make(1, title: title)));
    }

    [Theory]
    [InlineData("consent")]
    [InlineData("LockApp")]
    [InlineData("explorer")]
    [InlineData("EXPLORER")]
    [InlineData("ShellExperienceHost")]
    [InlineData("StartMenuExperienceHost")]
    [InlineData("SearchHost")]
    public void System_processes_are_untouchable(string process)
    {
        Assert.False(Policy().CanTouch(Windows.Make(1, process)));
    }

    [Theory]
    [InlineData("DeskPet")]
    [InlineData("deskpet")]
    public void Own_process_is_untouchable(string process)
    {
        Assert.False(Policy().CanTouch(Windows.Make(1, process)));
    }

    [Theory]
    [InlineData("Spotify")]
    [InlineData("spotify")]
    public void Allow_listed_processes_are_untouchable(string process)
    {
        var policy = Policy(new Settings { AllowList = ["Spotify"] });

        Assert.False(policy.CanTouch(Windows.Make(1, process)));
    }

    [Fact]
    public void Exe_suffix_in_settings_is_ignored()
    {
        var policy = Policy(new Settings { AllowList = ["Spotify.exe"] });

        Assert.False(policy.CanTouch(Windows.Make(1, "spotify")));
    }

    [Theory]
    [InlineData("1Password")]
    [InlineData("keepassxc")]
    public void Ignore_listed_processes_are_untouchable(string process)
    {
        Assert.False(Policy().CanTouch(Windows.Make(1, process)));
    }

    [Theory]
    [InlineData("My Bank - Chrome")]
    [InlineData("Change PASSWORD")]
    [InlineData("New InPrivate tab - Edge")]
    public void Titles_with_ignored_keywords_are_untouchable(string title)
    {
        Assert.False(Policy().CanTouch(Windows.Make(1, title: title)));
    }

    [Fact]
    public void Blank_keywords_do_not_block_everything()
    {
        var policy = Policy(new Settings { IgnoreTitleKeywords = ["", "  "], AllowList = [""] });

        Assert.True(policy.CanTouch(Windows.Make(1, title: "Anything")));
    }

    [Fact]
    public void Fullscreen_windows_are_untouchable()
    {
        Assert.False(Policy().CanTouch(Windows.Fullscreen(1)));
    }

    [Fact]
    public void Maximised_windows_remain_touchable()
    {
        var maximised = Windows.Make(1, bounds: new ScreenRect(0, 0, 1920, 1040), maximized: true);

        Assert.True(Policy().CanTouch(maximised));
    }

    [Fact]
    public void Zero_handle_is_untouchable()
    {
        Assert.False(Policy().CanTouch(Windows.Make(0)));
    }

    [Fact]
    public void Marked_handles_become_untouchable()
    {
        var policy = Policy();
        policy.MarkUntouchable(7);

        Assert.False(policy.CanTouch(Windows.Make(7)));
        Assert.True(policy.CanTouch(Windows.Make(8)));
    }

    [Fact]
    public void WithSettings_keeps_marked_handles_and_applies_new_lists()
    {
        var policy = Policy();
        policy.MarkUntouchable(7);
        var updated = policy.WithSettings(new Settings { AllowList = ["notepad"] });

        Assert.False(updated.CanTouch(Windows.Make(7)));
        Assert.False(updated.CanTouch(Windows.Make(8, "notepad")));
        Assert.False(updated.CanTouch(Windows.Make(9, "DeskPet")));
        Assert.True(policy.CanTouch(Windows.Make(8, "notepad")));
    }

    [Fact]
    public void Actions_blocked_when_paused()
    {
        Assert.False(Policy().ActionsAllowed(Windows.Make(1), paused: true));
    }

    [Fact]
    public void Actions_blocked_when_foreground_is_fullscreen()
    {
        Assert.False(Policy().ActionsAllowed(Windows.Fullscreen(1), paused: false));
    }

    [Fact]
    public void Actions_allowed_for_normal_or_missing_foreground()
    {
        Assert.True(Policy().ActionsAllowed(Windows.Make(1), paused: false));
        Assert.True(Policy().ActionsAllowed(null, paused: false));
    }

    [Fact]
    public void Minimised_window_covering_monitor_is_not_fullscreen()
    {
        var minimised = Windows.Make(1, bounds: Windows.Monitor, minimized: true);

        Assert.True(Policy().ActionsAllowed(minimised, paused: false));
    }
}
