using DeskPet.Platform;
using Godot;

namespace DeskPet.Pet;

/// <summary>
/// Owns where the pet stands on the desktop (physical pixels) and walks its window there with
/// tweens. The pet's feet sit at <see cref="FeetOffset"/> inside the art canvas.
/// </summary>
public partial class PetMover : Node
{
    public static readonly Vector2 CanvasSize = new(160, 160);
    public static readonly Vector2 FeetOffset = new(80, 150);

    /// <summary>Walking speed in art units per second, so it looks the same at any DPI.</summary>
    private const float WalkSpeed = 70f;

    private OwnWindow _window = null!;
    private Tween? _walk;
    private Vector2 _feet;
    private float _scale = 1f;

    /// <summary>Physical pixels per art unit on the pet's current monitor.</summary>
    public float Scale => _scale;

    public Vector2 Feet => _feet;

    public bool IsWalking => _walk?.IsRunning() == true;

    internal void Init(OwnWindow window) => _window = window;

    public void PlaceAt(Vector2 feet)
    {
        _scale = _window.PhysicalWidth / CanvasSize.X;
        SetFeet(feet);
    }

    /// <summary>Walks horizontally to <paramref name="x"/>; eases in and out like a small creature would.</summary>
    public void WalkTo(float x, Action arrived)
    {
        _walk?.Kill();
        float seconds = Mathf.Max(0.4f, Mathf.Abs(x - _feet.X) / (WalkSpeed * _scale));
        _walk = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _walk.TweenMethod(Callable.From<float>(SetFeetX), _feet.X, x, seconds);
        _walk.TweenCallback(Callable.From(arrived));
    }

    public void Stop() => _walk?.Kill();

    private void SetFeetX(float x) => SetFeet(new Vector2(x, _feet.Y));

    private void SetFeet(Vector2 feet)
    {
        _feet = feet;
        Vector2 topLeft = feet - FeetOffset * _scale;
        _window.MoveTo(Mathf.RoundToInt(topLeft.X), Mathf.RoundToInt(topLeft.Y));
    }
}
