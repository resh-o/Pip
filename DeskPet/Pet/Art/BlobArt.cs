using DeskPet.Core.Models;
using Godot;

namespace DeskPet.Pet.Art;

/// <summary>Procedural placeholder pet: one shader-drawn blob, animated entirely by tweens.</summary>
public partial class BlobArt : PetArt
{
    private const float PoseSeconds = 0.35f;
    private const float BodyWidth = 110f;
    private const float BodyHeight = 100f;
    private Node2D _body = null!;
    private Node2D _shadow = null!;
    private BlobSkin _skin = null!;
    private Tween? _pose;
    private Tween? _motion;
    private PetState _state = PetState.Idle;
    private float _mood = 1f;
    private float _facing = 1f;
    private Vector2 _look;
    private Vector2 _lookTarget;

    public override void _Ready()
    {
        _body = GetNode<Node2D>("Body");
        _shadow = GetNode<Node2D>("Shadow");
        _skin = new BlobSkin((ShaderMaterial)GetNode<CanvasItem>("Body/Skin").Material);
        ShowState(PetState.Idle);
        ScheduleBlink();
    }

    public override void _Process(double delta)
    {
        // Exponential smoothing keeps eye tracking fluid at any frame rate.
        Vector2 next = _look.Lerp(_lookTarget, 1f - Mathf.Exp(-10f * (float)delta));
        if (next.DistanceSquaredTo(_look) < 1e-6f)
            return;
        _look = next;
        _skin.SetLook(_look);
    }

    public override void ShowState(PetState state)
    {
        _state = state;
        _pose?.Kill();
        _motion?.Kill();
        var pose = BlobPose.For(state);
        _pose = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        TweenSkin(SkinParam.EyeOpen, pose.EyeOpen);
        TweenSkin(SkinParam.Happy, pose.Happy);
        TweenSkin(SkinParam.Droop, pose.Droop);
        TweenSkin(SkinParam.Smile, pose.Smile);
        TweenSkin(SkinParam.Blush, pose.Blush);
        TweenSkin(SkinParam.Blink, pose.Blink);
        TweenSkin(SkinParam.Saturation, pose.Saturation * MoodSaturation());
        _pose.TweenProperty(_body, "scale", pose.Scale, PoseSeconds).SetTrans(Tween.TransitionType.Back);
        _pose.TweenProperty(_body, "rotation", pose.Lean * _facing, PoseSeconds);
        _pose.TweenProperty(_body, "position:y", 0f, PoseSeconds);
        _pose.TweenProperty(_shadow, "scale", Vector2.One, PoseSeconds);
        _pose.Chain().TweenCallback(Callable.From(() => _motion = BlobMotion.Start(this, state, _body, _shadow, pose.Scale)));
    }

    public override void SetMood(float focus)
    {
        _mood = Mathf.Clamp(focus, 0f, 1f);
        _skin.TweenTo(CreateTween(), SkinParam.Saturation, BlobPose.For(_state).Saturation * MoodSaturation(), 1.0);
    }

    public override void LookToward(Vector2 direction) => _lookTarget = direction.LimitLength(1f);

    public override void SetFacing(float direction)
    {
        _facing = direction < 0 ? -1f : 1f;
        CreateTween().TweenProperty(_body, "rotation", BlobPose.For(_state).Lean * _facing, 0.2);
    }

    public override void GetEnvelope(Vector2[] points)
    {
        // An ellipse around the tallest, widest shape the state's motion can reach, plus the shadow.
        float top = BodyHeight * 1.15f + BlobMotion.LiftFor(_state) + 4f;
        float halfWidth = BodyWidth * 0.5f * 1.2f + 4f;
        var centre = new Vector2(0f, (8f - top) * 0.5f);
        var radii = new Vector2(halfWidth, (top + 8f) * 0.5f) * 1.06f;
        for (int i = 0; i < points.Length; i++)
            points[i] = centre + Vector2.FromAngle(Mathf.Tau * i / points.Length) * radii;
    }

    private float MoodSaturation() => Mathf.Lerp(0.75f, 1f, _mood);

    private void TweenSkin(SkinParam param, float value) => _skin.TweenTo(_pose!, param, value, PoseSeconds);

    private void ScheduleBlink() =>
        GetTree().CreateTimer(GD.RandRange(2.5, 6.0)).Timeout += Blink;

    private void Blink()
    {
        if (_state is not (PetState.Sleeping or PetState.Celebrating))
        {
            var tween = CreateTween().SetTrans(Tween.TransitionType.Sine);
            _skin.TweenTo(tween, SkinParam.Blink, 1f, 0.08);
            _skin.TweenBetween(tween, SkinParam.Blink, 1f, 0f, 0.12);
        }
        ScheduleBlink();
    }
}
