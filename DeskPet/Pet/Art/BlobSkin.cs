using Godot;

namespace DeskPet.Pet.Art;

internal enum SkinParam
{
    EyeOpen,
    Happy,
    Droop,
    Smile,
    Blush,
    Blink,
    Saturation,
}

/// <summary>
/// Typed access to blob.gdshader's uniforms. Values are mirrored here so tweens can start from the
/// current value without reading back through Variant, and so no property-path lookups are needed.
/// </summary>
internal sealed class BlobSkin
{
    private static readonly StringName[] Names =
        ["eye_open", "happy", "droop", "smile", "blush", "blink", "saturation"];

    private static readonly StringName LookName = "look";

    private readonly ShaderMaterial _material;
    private readonly float[] _values = [1f, 0f, 0f, 0.3f, 0f, 0f, 1f];

    public BlobSkin(ShaderMaterial material) => _material = material;

    public void Set(SkinParam param, float value)
    {
        _values[(int)param] = value;
        _material.SetShaderParameter(Names[(int)param], value);
    }

    public void SetLook(Vector2 look) => _material.SetShaderParameter(LookName, look);

    /// <summary>Tweens from the current value; only valid for the first step touching this parameter.</summary>
    public MethodTweener TweenTo(Tween tween, SkinParam param, float value, double seconds) =>
        tween.TweenMethod(Callable.From<float>(v => Set(param, v)), _values[(int)param], value, seconds);

    /// <summary>Tweens between explicit values, for steps chained after another step on the same parameter.</summary>
    public MethodTweener TweenBetween(Tween tween, SkinParam param, float from, float to, double seconds) =>
        tween.TweenMethod(Callable.From<float>(v => Set(param, v)), from, to, seconds);
}
