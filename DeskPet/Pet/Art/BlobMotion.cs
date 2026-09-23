using DeskPet.Core.Models;
using Godot;

namespace DeskPet.Pet.Art;

/// <summary>Looping body motion per state, built as tweens on the Body and Shadow nodes.</summary>
internal static class BlobMotion
{
    /// <summary>How far above the feet a state's motion can lift the body (art units).</summary>
    public static float LiftFor(PetState state) => state switch
    {
        PetState.Walking => 12f,
        PetState.Celebrating => 30f,
        PetState.Alert => 14f,
        _ => 0f,
    };

    public static Tween Start(Node owner, PetState state, Node2D body, Node2D shadow, Vector2 rest) => state switch
    {
        PetState.Walking => Hop(owner, body, shadow, rest, 12f, 0.42f),
        PetState.Celebrating => Hop(owner, body, shadow, rest, 30f, 0.55f),
        PetState.Alert => Startle(owner, body, rest),
        PetState.Grabbing => Breathe(owner, body, rest, 0.06f, 0.5f),
        PetState.Dragging => Strain(owner, body),
        PetState.Sleeping => Breathe(owner, body, rest, 0.045f, 5f),
        PetState.Sad => Breathe(owner, body, rest, 0.02f, 4f),
        _ => Breathe(owner, body, rest, 0.03f, 3f),
    };

    private static Tween Breathe(Node owner, Node2D body, Vector2 rest, float amount, float period)
    {
        var tween = Loop(owner);
        var inhale = rest * new Vector2(1f - amount * 0.5f, 1f + amount);
        tween.TweenProperty(body, "scale", inhale, period * 0.5f);
        tween.TweenProperty(body, "scale", rest, period * 0.5f);
        return tween;
    }

    // Squash on landing, stretch in the air; the shadow shrinks as the body rises.
    private static Tween Hop(Node owner, Node2D body, Node2D shadow, Vector2 rest, float height, float period)
    {
        var tween = Loop(owner);
        float up = period * 0.45f, down = period * 0.35f, land = period * 0.2f;
        tween.TweenProperty(body, "position:y", -height, up).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(body, "scale", rest * new Vector2(0.92f, 1.1f), up);
        tween.Parallel().TweenProperty(shadow, "scale", new Vector2(0.75f, 0.75f), up);
        tween.TweenProperty(body, "position:y", 0f, down).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(shadow, "scale", Vector2.One, down);
        tween.TweenProperty(body, "scale", rest * new Vector2(1.14f, 0.86f), land * 0.5f);
        tween.TweenProperty(body, "scale", rest, land * 0.5f);
        return tween;
    }

    private static Tween Startle(Node owner, Node2D body, Vector2 rest)
    {
        var tween = owner.CreateTween().SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(body, "position:y", -14f, 0.12f).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(body, "position:y", 0f, 0.16f).SetEase(Tween.EaseType.In);
        tween.TweenProperty(body, "scale", rest * new Vector2(1.1f, 0.9f), 0.06f);
        tween.TweenProperty(body, "scale", rest, 0.14f).SetTrans(Tween.TransitionType.Back);
        return tween;
    }

    private static Tween Strain(Node owner, Node2D body)
    {
        var tween = Loop(owner);
        float lean = body.Rotation;
        tween.TweenProperty(body, "rotation", lean - 0.04f, 0.09f);
        tween.TweenProperty(body, "rotation", lean + 0.02f, 0.09f);
        return tween;
    }

    private static Tween Loop(Node owner) =>
        owner.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
}
