using DeskPet.Core.Models;
using Godot;

namespace DeskPet.Pet;

/// <summary>
/// Contract every pet art scene's root implements. The node's origin is the point between the
/// pet's feet; to swap art, point PetView's ArtScene at another scene deriving from this.
/// </summary>
public abstract partial class PetArt : Node2D
{
    /// <summary>Tweens into the pose and looping motion for a state.</summary>
    public abstract void ShowState(PetState state);

    /// <summary>Focus score 0..1; tints the pet's overall mood.</summary>
    public abstract void SetMood(float focus);

    /// <summary>Where the eyes should look, as a direction in art space (length ≤ 1).</summary>
    public abstract void LookToward(Vector2 direction);

    /// <summary>-1 facing left, 1 facing right.</summary>
    public abstract void SetFacing(float direction);

    /// <summary>
    /// Fills <paramref name="points"/> with a polygon (art space) enclosing everything the current
    /// state may draw. On Windows pixels outside the mouse region are not drawn, so this must be
    /// generous enough to cover whole animations, not just the current frame.
    /// </summary>
    public abstract void GetEnvelope(Vector2[] points);
}
