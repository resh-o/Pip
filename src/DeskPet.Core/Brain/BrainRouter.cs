using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Core.Brain;

/// <summary>
/// The brain the app uses: cache first, then one batched call to the primary (Claude) brain for
/// sendable cache misses, with the offline rules covering everything the primary can't or mustn't judge.
/// </summary>
public sealed class BrainRouter : IBrain
{
    private readonly IBrain _primary;
    private readonly RuleBrain _rules;
    private readonly VerdictCache _cache;
    private readonly ApiRateLimiter _limiter;
    private readonly ISettings _settings;
    private readonly object _goalGate = new();
    private string? _cachedGoal;

    public BrainRouter(IBrain primary, RuleBrain rules, VerdictCache cache, ApiRateLimiter limiter, ISettings settings)
    {
        _primary = primary;
        _rules = rules;
        _cache = cache;
        _limiter = limiter;
        _settings = settings;
    }

    /// <summary>Stops primary calls for a while, e.g. after the API answered 429 with retry-after.</summary>
    public void Backoff(TimeSpan duration) => _limiter.PauseFor(duration);

    public async Task<IReadOnlyList<Verdict>> JudgeAsync(Goal goal, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
    {
        var goalKey = SwitchGoal(goal);
        var results = new Verdict[windows.Count];
        var keys = windows.Select(w => TitlePattern.Create(w.ProcessName, w.Title)).ToArray();
        var misses = new List<int>();
        var canUsePrimary = _settings.Current.PrivacyAcknowledged && !goal.IsEmpty;

        for (var i = 0; i < windows.Count; i++)
        {
            if (_cache.TryGet(keys[i], out var cached))
            {
                results[i] = cached;
            }
            else if (canUsePrimary && misses.Count < PromptBuilder.MaxWindows && PrivacyFilter.IsSendable(_settings.Current, windows[i]))
            {
                misses.Add(i);
            }
            else
            {
                results[i] = _rules.Judge(goal, windows[i]);
            }
        }

        if (misses.Count > 0)
        {
            await JudgeMissesAsync(goal, goalKey, windows, keys, misses, results, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    private async Task JudgeMissesAsync(
        Goal goal, string goalKey, IReadOnlyList<WindowInfo> windows, string[] keys, List<int> misses, Verdict[] results, CancellationToken cancellationToken)
    {
        var batch = misses.Select(i => windows[i]).ToArray();
        var verdicts = await TryPrimaryAsync(goal, batch, cancellationToken).ConfigureAwait(false);
        var cacheable = verdicts is not null && IsCurrentGoal(goalKey);

        for (var m = 0; m < misses.Count; m++)
        {
            var i = misses[m];
            if (verdicts is null)
            {
                // Not cached, so the window gets another chance at the primary on a later tick.
                results[i] = _rules.Judge(goal, windows[i]);
                continue;
            }

            results[i] = verdicts[m];
            if (cacheable)
            {
                _cache.Set(keys[i], verdicts[m]);
            }
        }
    }

    /// <summary>Null means "use the rules": rate-limited, failed, timed out or answered with the wrong count.</summary>
    private async Task<IReadOnlyList<Verdict>?> TryPrimaryAsync(Goal goal, WindowInfo[] batch, CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        if (!_limiter.TryAcquire(settings.MaxApiCallsPerMinute))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ApiTimeoutSeconds));
        try
        {
            // WaitAsync enforces the timeout even if the primary ignores its token.
            var verdicts = await _primary.JudgeAsync(goal, batch, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
            return verdicts.Count == batch.Length ? verdicts : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Any primary failure is non-fatal by design: the pet keeps working offline.
            return null;
        }
    }

    private string SwitchGoal(Goal goal)
    {
        var key = goal.Text.Trim();
        lock (_goalGate)
        {
            if (!string.Equals(_cachedGoal, key, StringComparison.Ordinal))
            {
                _cache.Reset();
                _cachedGoal = key;
            }
        }

        return key;
    }

    // A goal change during an in-flight call would otherwise poison the fresh cache with stale verdicts.
    private bool IsCurrentGoal(string goalKey)
    {
        lock (_goalGate)
        {
            return string.Equals(_cachedGoal, goalKey, StringComparison.Ordinal);
        }
    }
}
