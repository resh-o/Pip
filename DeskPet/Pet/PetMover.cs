using DeskPet.Platform;
using Godot;

namespace DeskPet.Pet;

/// <summary>
/// Owns where the pet stands on the desktop (physical pixels) and moves its window there with
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

    /// <summary>Puts the feet at a physical screen point immediately (also used to ride a dragged window).</summary>
    public void PlaceAt(Vector2 feet)
    {
        // DWM rescales the window on monitors of another DPI, so re-read its real size each move.
        _scale = _window.PhysicalWidth / CanvasSize.X;
        _feet = feet;
        Vector2 topLeft = feet - FeetOffset * _scale;
        _window.MoveTo(Mathf.RoundToInt(topLeft.X), Mathf.RoundToInt(topLeft.Y));
    }

    /// <summary>Walks (or hops through the air) to <paramref name="feet"/>, easing in and out; a job caps the time so it hurries.</summary>
    public void TravelTo(Vector2 feet, Action arrived, float speedFactor = 1f, float maxSeconds = float.MaxValue)
    {
        _walk?.Kill();
        Vector2 from = _feet;
        float seconds = Mathf.Clamp(from.DistanceTo(feet) / (WalkSpeed * speedFactor * _scale), 0.35f, Mathf.Max(0.35f, maxSeconds));
        _walk = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _walk.TweenMethod(Callable.From<float>(t => PlaceAt(from.Lerp(feet, t))), 0f, 1f, seconds);
        _walk.TweenCallback(Callable.From(arrived));
    }

    /// <summary>The floor directly below the pet: the bottom of the work area it is over.</summary>
    public Vector2 FloorBelow()
    {
        var work = Desktop.WorkAreaAt(Mathf.RoundToInt(_feet.X), Mathf.RoundToInt(_feet.Y));
        float margin = CanvasSize.X * 0.5f * _scale;
        return new Vector2(Mathf.Clamp(_feet.X, work.Left + margin, work.Right - margin), work.Bottom);
    }

    public void Stop() => _walk?.Kill();
}
