using DeskPet.Core.Config;
using DeskPet.Core.Sensing;
using DeskPet.Core.Tests.Fakes;

namespace DeskPet.Core.Tests.Sensing;

public sealed class ForegroundDebouncerTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    private readonly FakeClock _clock = new();
    private readonly TestSettings _settings = new(new Settings { DwellSeconds = 1.5, EnumerateSeconds = 30 });
    private readonly ForegroundDebouncer _debouncer;

    public ForegroundDebouncerTests() => _debouncer = new ForegroundDebouncer(_clock, _settings);

    [Fact]
    public void Does_not_judge_before_dwell()
    {
        var window = Windows.Make(1);

        Assert.Equal(DebounceResult.None, _debouncer.Observe(window));
        _clock.Advance(TimeSpan.FromSeconds(1.25));
        Assert.Equal(DebounceResult.None, _debouncer.Observe(window));
    }

    [Fact]
    public void Judges_and_enumerates_once_dwell_reached()
    {
        var window = Windows.Make(1);
        _debouncer.Observe(window);
        _clock.Advance(TimeSpan.FromSeconds(1.5));

        Assert.Equal(DebounceResult.Judge | DebounceResult.Enumerate, _debouncer.Observe(window));
    }

    [Fact]
    public void Judges_only_once_per_dwell()
    {
        var window = Windows.Make(1);
        var judgements = PollFor(window, TimeSpan.FromSeconds(10), DebounceResult.Judge);

        Assert.Equal(1, judgements);
    }

    [Fact]
    public void Handle_change_restarts_dwell()
    {
        _debouncer.Observe(Windows.Make(1));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(DebounceResult.None, _debouncer.Observe(Windows.Make(2)));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(DebounceResult.None, _debouncer.Observe(Windows.Make(2)));
        _clock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.True(_debouncer.Observe(Windows.Make(2)).HasFlag(DebounceResult.Judge));
    }

    [Fact]
    public void Title_change_on_same_handle_counts_as_new_foreground()
    {
        var tabA = Windows.Make(1, title: "Docs - Browser");
        var tabB = Windows.Make(1, title: "YouTube - Browser");
        PollFor(tabA, TimeSpan.FromSeconds(2), DebounceResult.Judge);

        Assert.Equal(DebounceResult.None, _debouncer.Observe(tabB));
        _clock.Advance(TimeSpan.FromSeconds(1.5));
        Assert.Equal(DebounceResult.Judge | DebounceResult.Enumerate, _debouncer.Observe(tabB));
    }

    [Fact]
    public void Returning_to_a_window_after_a_switch_judges_it_again()
    {
        var a = Windows.Make(1);
        PollFor(a, TimeSpan.FromSeconds(2), DebounceResult.Judge);
        _debouncer.Observe(Windows.Make(2));
        _clock.Advance(Poll);

        Assert.Equal(1, PollFor(a, TimeSpan.FromSeconds(2), DebounceResult.Judge));
    }

    [Fact]
    public void Null_foreground_resets_dwell()
    {
        var window = Windows.Make(1);
        _debouncer.Observe(window);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _debouncer.Observe(null);
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(DebounceResult.None, _debouncer.Observe(window));
    }

    [Fact]
    public void Null_foreground_is_never_judged()
    {
        Assert.Equal(0, PollFor(null, TimeSpan.FromSeconds(5), DebounceResult.Judge));
    }

    [Fact]
    public void Enumerates_periodically_on_a_stable_window()
    {
        var window = Windows.Make(1);
        _debouncer.Observe(window);
        _clock.Advance(TimeSpan.FromSeconds(2));
        _debouncer.Observe(window); // judge + enumerate

        _clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(DebounceResult.None, _debouncer.Observe(window));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(DebounceResult.Enumerate, _debouncer.Observe(window));
        _clock.Advance(Poll);
        Assert.Equal(DebounceResult.None, _debouncer.Observe(window));
    }

    [Fact]
    public void Enumerates_periodically_even_with_no_foreground()
    {
        Assert.Equal(1, PollFor(null, TimeSpan.FromSeconds(31), DebounceResult.Enumerate));
    }

    [Fact]
    public void Judge_resets_the_enumeration_timer()
    {
        var window = Windows.Make(1);
        _clock.Advance(TimeSpan.FromSeconds(20));
        _debouncer.Observe(window);
        _clock.Advance(TimeSpan.FromSeconds(2));
        _debouncer.Observe(window); // judge + enumerate at t=22

        _clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(DebounceResult.None, _debouncer.Observe(window));
    }

    [Fact]
    public void Uses_current_settings()
    {
        _settings.Current = _settings.Current with { DwellSeconds = 0.5 };
        var window = Windows.Make(1);
        _debouncer.Observe(window);
        _clock.Advance(TimeSpan.FromSeconds(0.5));

        Assert.True(_debouncer.Observe(window).HasFlag(DebounceResult.Judge));
    }

    [Fact]
    public void Observe_does_not_allocate()
    {
        var a = Windows.Make(1);
        var b = Windows.Make(2);
        _debouncer.Observe(a); // warm up

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            _clock.Advance(Poll);
            _debouncer.Observe(i % 20 < 10 ? a : b);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private int PollFor(Models.WindowInfo? window, TimeSpan duration, DebounceResult flag)
    {
        var count = 0;
        for (var elapsed = TimeSpan.Zero; elapsed <= duration; elapsed += Poll)
        {
            if (_debouncer.Observe(window).HasFlag(flag))
            {
                count++;
            }

            _clock.Advance(Poll);
        }

        return count;
    }
}
