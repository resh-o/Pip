using DeskPet.Core.Models;
using Godot;

namespace DeskPet.Pet;

/// <summary>
/// Keeps CPU low by dropping to ~10 FPS in calm states, but stays smooth briefly after any state
/// change so pose transitions still ease rather than step.
/// </summary>
internal sealed class FramePacer
{
    private const int CalmFps = 10;
    private const double SettleSeconds = 0.6;

    private readonly int _smoothFps;
    private double _smoothFor;
    private bool _calm = true;

    public FramePacer()
    {
        float refresh = DisplayServer.ScreenGetRefreshRate();
        _smoothFps = refresh > 0 ? Mathf.RoundToInt(refresh) : 60;
    }

    public void OnStateChanged(PetState state)
    {
        _calm = state is PetState.Idle or PetState.Working or PetState.Sad or PetState.Sleeping;
        _smoothFor = SettleSeconds;
        Engine.MaxFps = _smoothFps;
    }

    /// <summary>Walking moves the window itself, which must stay smooth whatever the state says.</summary>
    public void Update(double delta, bool moving)
    {
        if (moving)
        {
            _smoothFor = SettleSeconds;
            Engine.MaxFps = _smoothFps;
            return;
        }
        if (_smoothFor <= 0)
            return;
        _smoothFor -= delta;
        if (_smoothFor <= 0 && _calm)
            Engine.MaxFps = CalmFps;
    }
}
