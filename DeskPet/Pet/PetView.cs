using DeskPet.Core.Models;
using Godot;

namespace DeskPet.Pet;

/// <summary>Hosts the swappable art scene and translates screen-space intent into art-space calls.</summary>
public partial class PetView : Node2D
{
    /// <summary>Distance (art units) at which the eyes reach their full sideways travel.</summary>
    private const float LookRange = 160f;

    [Export] public PackedScene? ArtScene { get; set; }

    public PetArt Art { get; private set; } = null!;

    public override void _Ready()
    {
        Art = ArtScene?.Instantiate<PetArt>() ?? throw new InvalidOperationException("PetView needs an ArtScene deriving from PetArt.");
        AddChild(Art);
    }

    public void ShowState(PetState state) => Art.ShowState(state);

    public void SetMood(float focus) => Art.SetMood(focus);

    public void SetFacing(float direction) => Art.SetFacing(direction);

    /// <summary>Points the eyes at a target given as an offset from the pet's eyes, in art units.</summary>
    public void LookToward(Vector2 offset) => Art.LookToward(offset / (offset.Length() + LookRange) * 1.4f);
}
