using DeskPet.Core.Models;
using DeskPet.Pet;
using Godot;

// TEMPORARY review harness; never committed.
public partial class Demo : Node
{
    private static readonly PetState[] States =
        [PetState.Celebrating];
    private static readonly PetState[] Unused =
        [PetState.Idle,
         PetState.Walking, PetState.Grabbing, PetState.Dragging, PetState.Celebrating];
    private int _i;

    public override void _Ready() => GetTree().CreateTimer(1.0).Timeout += Next;

    private void Next()
    {
        if (_i >= States.Length) return;
        GetNode<PetView>("../Pet").ShowState(States[_i]);
        GD.Print($"DEMO {States[_i]} {Time.GetTicksMsec()}");
        _i++;
        GetTree().CreateTimer(3.0).Timeout += Next;
    }
}
