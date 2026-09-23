using DeskPet.Core.Models;
using Godot;

namespace DeskPet.Pet.Art;

/// <summary>Resting face and body shape the blob eases into for each state.</summary>
internal readonly record struct BlobPose(
    float EyeOpen, float Happy, float Droop, float Smile, float Blush, float Blink,
    float Saturation, Vector2 Scale, float Lean)
{
    private static readonly Vector2 Neutral = Vector2.One;

    public static BlobPose For(PetState state) => state switch
    {
        PetState.Working => new(0.9f, 0f, 0f, 0.55f, 0f, 0f, 1f, Neutral, 0f),
        PetState.Sad => new(0.85f, 0f, 1f, -0.6f, 0f, 0f, 0.7f, new(1.08f, 0.88f), 0f),
        PetState.Sleeping => new(1f, 0f, 0.6f, 0f, 0.3f, 1f, 0.85f, new(1.1f, 0.86f), 0f),
        PetState.Alert => new(1.35f, 0f, 0f, -0.15f, 0f, 0f, 1f, new(0.94f, 1.08f), 0.08f),
        PetState.Walking => new(1f, 0f, 0f, 0.35f, 0f, 0f, 1f, Neutral, 0.1f),
        PetState.Grabbing => new(1.1f, 0f, 0f, 0.05f, 0f, 0f, 1f, new(0.96f, 1.05f), 0.12f),
        PetState.Dragging => new(0.8f, 0f, 0.2f, 0.2f, 0.4f, 0f, 1f, new(1.04f, 0.96f), -0.2f),
        PetState.Celebrating => new(1f, 1f, 0f, 1f, 1f, 0f, 1f, Neutral, 0f),
        _ => new(1f, 0f, 0f, 0.3f, 0f, 0f, 1f, Neutral, 0f),
    };
}
