using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;
using DeskPet.Core.Sensing;
using DeskPet.Pet;

namespace DeskPet.Platform;

/// <summary>
/// Background sensing loop: polls the foreground window, debounces it, re-enumerates and judges
/// windows when needed, and posts each tick to the main thread. Never touches Godot objects.
/// </summary>
internal sealed class ForegroundPoller : IDisposable
{
    private readonly IWindowService _windows;
    private readonly IBrain _brain;
    private readonly ForegroundDebouncer _debouncer;
    private readonly ISettings _settings;
    private readonly Action<Observation> _post;
    private readonly CancellationTokenSource _stop = new();
    private IReadOnlyList<WindowInfo> _enumerated = [];

    public ForegroundPoller(IWindowService windows, IBrain brain, ForegroundDebouncer debouncer, ISettings settings, Action<Observation> post)
    {
        _windows = windows;
        _brain = brain;
        _debouncer = debouncer;
        _settings = settings;
        _post = post;
    }

    public void Start() => _ = Task.Run(RunAsync);

    public void Dispose() => _stop.Cancel();

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.Current.ForegroundPollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                try
                {
                    _post(await PollAsync(_stop.Token));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    DiagnosticLog.Write($"poller: {e.GetType().Name}: {e.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task<Observation> PollAsync(CancellationToken token)
    {
        var foreground = _windows.GetForeground();
        var result = _debouncer.Observe(foreground);
        if (result == DebounceResult.None)
            return new Observation(foreground, null, null);

        if (result.HasFlag(DebounceResult.Enumerate))
            _enumerated = _windows.Enumerate();
        var batch = WithForeground(_enumerated, foreground, out int foregroundIndex);
        var verdicts = await _brain.JudgeAsync(new Goal(_settings.Current.Goal), batch, token);

        var known = new (WindowInfo, Verdict)[_enumerated.Count];
        for (int i = 0; i < known.Length; i++)
            known[i] = (batch[i], verdicts[i]);
        Verdict? judged = result.HasFlag(DebounceResult.Judge) && foregroundIndex >= 0 ? verdicts[foregroundIndex] : null;
        return new Observation(foreground, judged, known);
    }

    // The foreground may be missing from the last enumeration (a new window); judge it in the same call.
    private static IReadOnlyList<WindowInfo> WithForeground(IReadOnlyList<WindowInfo> windows, WindowInfo? foreground, out int index)
    {
        index = -1;
        if (foreground is null)
            return windows;
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].Handle == foreground.Handle)
            {
                index = i;
                return windows;
            }
        }
        var batch = new List<WindowInfo>(windows.Count + 1);
        batch.AddRange(windows);
        batch.Add(foreground);
        index = batch.Count - 1;
        return batch;
    }
}
