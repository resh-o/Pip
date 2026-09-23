using DeskPet.Pet;
using Godot;

namespace DeskPet.Ui;

/// <summary>
/// Makes the window click-through everywhere except the pet. On Windows this is a window region,
/// which also clips drawing, so the art's envelope (not its current frame) is used, and the
/// region is only rebuilt when it moves by more than a couple of pixels.
/// </summary>
internal sealed class PassthroughUpdater
{
    private const float Tolerance = 2f;

    private readonly Window _window;
    private readonly PetArt _art;
    private readonly Vector2[] _local = new Vector2[20];
    private readonly Vector2[] _applied = new Vector2[20];
    private bool _hasApplied;

    public PassthroughUpdater(Window window, PetArt art)
    {
        _window = window;
        _art = art;
    }

    public void Refresh()
    {
        _art.GetEnvelope(_local);
        Transform2D toWindow = _window.GetStretchTransform() * _art.GetGlobalTransformWithCanvas();
        bool changed = !_hasApplied;
        for (int i = 0; i < _local.Length; i++)
        {
            Vector2 p = toWindow * _local[i];
            changed |= p.DistanceTo(_applied[i]) > Tolerance;
            _local[i] = p;
        }
        if (!changed)
            return;
        Array.Copy(_local, _applied, _local.Length);
        _hasApplied = true;
        _window.MousePassthroughPolygon = _applied;
    }
}
