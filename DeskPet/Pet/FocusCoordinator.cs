using DeskPet.Core.Focus;
using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.State;

namespace DeskPet.Pet;

/// <summary>Main-thread consumer of sensing: updates focus and mood, sleeps for fullscreen, asks the planner what to do.</summary>
internal sealed class FocusCoordinator
{
    private const double MoodEpsilon = 0.02;
    private static readonly IReadOnlyList<(WindowInfo Window, Verdict Verdict)> None = [];

    private readonly ActionPlanner _planner;
    private readonly FocusTracker _focus;
    private readonly PetController _pet;
    private readonly ActionRunner _runner;
    private IReadOnlyList<(WindowInfo Window, Verdict Verdict)> _known = None;
    private bool _fullscreen;
    private double _shownMood = -1;

    public FocusCoordinator(ActionPlanner planner, FocusTracker focus, PetController pet, ActionRunner runner)
    {
        _planner = planner;
        _focus = focus;
        _pet = pet;
        _runner = runner;
    }

    public bool Paused { get; set; }

    public void OnObservation(Observation observation)
    {
        UpdateFullscreen(observation.Foreground);
        if (observation.Known is { } known)
            _known = known;
        if (observation.ForegroundVerdict is { } verdict)
            _focus.Record(verdict);
        UpdateMood();

        PetAction? action = observation is { Foreground: { } foreground, ForegroundVerdict: { } judged }
            ? _planner.OnJudged(foreground, judged, _known, Paused)
            : _planner.OnTick(observation.Foreground, _known, Paused);
        if (action is not null)
            _runner.Execute(action);
    }

    private void UpdateFullscreen(WindowInfo? foreground)
    {
        bool fullscreen = foreground?.IsFullscreen == true;
        if (fullscreen == _fullscreen)
            return;
        _fullscreen = fullscreen;
        if (fullscreen)
            _runner.CancelAll();
        _pet.Machine.Fire(fullscreen ? PetEvent.FullscreenEntered : PetEvent.FullscreenExited);
    }

    private void UpdateMood()
    {
        if (_focus.UpdateBand())
            _pet.Machine.Fire(_focus.Band switch
            {
                FocusBand.High => PetEvent.FocusImproved,
                FocusBand.Low => PetEvent.FocusDropped,
                _ => PetEvent.FocusSettled,
            });
        double score = _focus.Score;
        if (Math.Abs(score - _shownMood) < MoodEpsilon)
            return;
        _shownMood = score;
        _pet.SetMood((float)score);
    }
}
