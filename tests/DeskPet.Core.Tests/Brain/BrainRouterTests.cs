using DeskPet.Core.Brain;
using DeskPet.Core.Config;
using DeskPet.Core.Models;
using DeskPet.Core.Tests.Fakes;
using static DeskPet.Core.Tests.Brain.BrainTestData;

namespace DeskPet.Core.Tests.Brain;

public sealed class BrainRouterTests
{
    private static readonly Goal Goal = new("Finish the report");

    private readonly FakeClock _clock = new();
    private readonly RecordingBrain _primary = new();
    private readonly StaticSettings _settings = new(new Settings { PrivacyAcknowledged = true, MaxApiCallsPerMinute = 100 });
    private readonly VerdictCache _cache;
    private readonly BrainRouter _router;

    public BrainRouterTests()
    {
        _cache = new VerdictCache(_clock);
        _router = new BrainRouter(_primary, new RuleBrain(_settings), _cache, new ApiRateLimiter(_clock), _settings);
    }

    private Task<IReadOnlyList<Verdict>> Judge(params WindowInfo[] windows) => _router.JudgeAsync(Goal, windows, CancellationToken.None);

    // An unknown window: rules say Neutral, the recording primary says OnTask, so results reveal who judged.
    private static WindowInfo Unknown(string title = "Some page") => Window("chrome", title);

    [Fact]
    public async Task MissesGoToPrimaryInOneBatchAndAreCached()
    {
        var verdicts = await Judge(Unknown("a"), Unknown("b"));

        Assert.Equal([Verdict.OnTask, Verdict.OnTask], verdicts);
        Assert.Equal(2, Assert.Single(_primary.Calls).Count);
        Assert.Equal(2, _cache.Count);
    }

    [Fact]
    public async Task CacheHitAvoidsPrimary()
    {
        await Judge(Unknown("Inbox (3)"));
        var verdicts = await Judge(Unknown("Inbox (4)"));

        Assert.Equal([Verdict.OnTask], verdicts);
        Assert.Single(_primary.Calls);
    }

    [Fact]
    public async Task OnlyMissesAreSentAndResultsKeepInputOrder()
    {
        await Judge(Unknown("warm"));
        _cache.Set(TitlePattern.Create("chrome", "cached"), Verdict.Distraction);
        _primary.Behaviour = (w, _) => Task.FromResult<IReadOnlyList<Verdict>>(w.Select(_ => Verdict.Neutral).ToArray());

        var verdicts = await Judge(Unknown("new"), Unknown("cached"), Unknown("warm"));

        Assert.Equal([Verdict.Neutral, Verdict.Distraction, Verdict.OnTask], verdicts);
        Assert.Equal("new", Assert.Single(_primary.Calls[^1]).Title);
    }

    [Fact]
    public async Task PrivacyNotAcknowledgedNeverCallsPrimary()
    {
        _settings.Current = _settings.Current with { PrivacyAcknowledged = false };

        var verdicts = await Judge(Unknown(), Window("Steam", "Store"));

        Assert.Equal([Verdict.Neutral, Verdict.Distraction], verdicts);
        Assert.Empty(_primary.Calls);
    }

    [Fact]
    public async Task EmptyGoalNeverCallsPrimary()
    {
        await _router.JudgeAsync(Goal.None, [Unknown()], CancellationToken.None);

        Assert.Empty(_primary.Calls);
    }

    [Fact]
    public async Task IgnoredWindowsAreNeverPassedToPrimaryNorCached()
    {
        var verdicts = await Judge(Window("KeePass", "db"), Window("chrome", "My bank"), Unknown("ok"));

        Assert.Equal([Verdict.Neutral, Verdict.Neutral, Verdict.OnTask], verdicts);
        Assert.Equal("ok", Assert.Single(Assert.Single(_primary.Calls)).Title);
        Assert.Equal(1, _cache.Count);
    }

    [Fact]
    public async Task RateCapFallsBackToRulesWithoutCaching()
    {
        _settings.Current = _settings.Current with { MaxApiCallsPerMinute = 2 };

        await Judge(Unknown("1"));
        await Judge(Unknown("a"));
        var verdicts = await Judge(Unknown("b"));

        Assert.Equal([Verdict.Neutral], verdicts);
        Assert.Equal(2, _primary.Calls.Count);
        Assert.Equal(2, _cache.Count);

        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal([Verdict.OnTask], await Judge(Unknown("b")));
    }

    [Fact]
    public async Task BackoffPausesPrimaryCalls()
    {
        _router.Backoff(TimeSpan.FromSeconds(30));

        Assert.Equal([Verdict.Neutral], await Judge(Unknown("a")));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal([Verdict.OnTask], await Judge(Unknown("a")));
        Assert.Single(_primary.Calls);
    }

    [Fact]
    public async Task ExceptionFallsBackToRulesAndRetriesLater()
    {
        _primary.Behaviour = (_, _) => throw new HttpRequestException("boom");

        Assert.Equal([Verdict.Neutral], await Judge(Unknown()));
        Assert.Equal(0, _cache.Count);

        _primary.Behaviour = new RecordingBrain().Behaviour;
        Assert.Equal([Verdict.OnTask], await Judge(Unknown()));
    }

    [Fact]
    public async Task FaultedTaskFallsBack()
    {
        _primary.Behaviour = (_, _) => Task.FromException<IReadOnlyList<Verdict>>(new InvalidDataException());

        Assert.Equal([Verdict.Neutral], await Judge(Unknown()));
    }

    [Fact]
    public async Task WrongVerdictCountFallsBack()
    {
        _primary.Behaviour = (_, _) => Task.FromResult<IReadOnlyList<Verdict>>([Verdict.OnTask]);

        Assert.Equal([Verdict.Neutral, Verdict.Neutral], await Judge(Unknown("a"), Unknown("b")));
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task TimeoutFallsBackEvenIfPrimaryIgnoresItsToken()
    {
        _settings.Current = _settings.Current with { ApiTimeoutSeconds = 0.05 };
        _primary.Behaviour = (_, _) => new TaskCompletionSource<IReadOnlyList<Verdict>>().Task;

        Assert.Equal([Verdict.Neutral], await Judge(Unknown()));
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task TimeoutCancelsPrimaryToken()
    {
        _settings.Current = _settings.Current with { ApiTimeoutSeconds = 0.05 };
        CancellationToken seen = default;
        _primary.Behaviour = async (w, ct) =>
        {
            seen = ct;
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        };

        Assert.Equal([Verdict.Neutral], await Judge(Unknown()));
        Assert.True(seen.IsCancellationRequested);
    }

    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        _primary.Behaviour = async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _router.JudgeAsync(Goal, [Unknown()], cts.Token));
    }

    [Fact]
    public async Task GoalChangeResetsCache()
    {
        await Judge(Unknown());
        await _router.JudgeAsync(new Goal("Something else"), [Unknown()], CancellationToken.None);

        Assert.Equal(2, _primary.Calls.Count);
    }

    [Fact]
    public async Task GoalChangeDuringCallDoesNotCacheStaleVerdicts()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<Verdict>>();
        _primary.Behaviour = (_, _) => gate.Task;
        var pending = Judge(Unknown());

        _primary.Behaviour = new RecordingBrain().Behaviour;
        await _router.JudgeAsync(new Goal("Other goal"), [Unknown("x")], CancellationToken.None);
        gate.SetResult([Verdict.Distraction]);

        Assert.Equal([Verdict.Distraction], await pending);
        Assert.False(_cache.TryGet(TitlePattern.Create("chrome", "Some page"), out _));
    }

    [Fact]
    public async Task BatchIsCappedAtTwentyAndRestUseRules()
    {
        var windows = Enumerable.Range(0, 25).Select(i => Unknown($"page {(char)('a' + i)}")).ToArray();

        var verdicts = await Judge(windows);

        Assert.Equal(PromptBuilder.MaxWindows, Assert.Single(_primary.Calls).Count);
        Assert.All(verdicts.Take(20), v => Assert.Equal(Verdict.OnTask, v));
        Assert.All(verdicts.Skip(20), v => Assert.Equal(Verdict.Neutral, v));
    }

    [Fact]
    public async Task NoWindowsNoCall()
    {
        Assert.Empty(await Judge());
        Assert.Empty(_primary.Calls);
    }
}
