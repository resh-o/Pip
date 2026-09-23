using DeskPet.Pet;
using DeskPet.Platform;
using DeskPet.Ui;
using Godot;

namespace DeskPet;

/// <summary>Composition root: sizes and hides the pet window, then wires the pet together.</summary>
public partial class App : Node
{
    public override void _Ready()
    {
        var window = new OwnWindow();
        window.HideFromTaskbar();
        int side = Mathf.RoundToInt(PetMover.CanvasSize.X * window.SystemScale);
        GetWindow().Size = new Vector2I(side, side);

        var view = GetNode<PetView>("Pet");
        var mover = GetNode<PetMover>("Mover");
        mover.Init(window);
        var (x, y) = Desktop.CursorPosition();
        var work = Desktop.WorkAreaAt(x, y);
        mover.PlaceAt(new Vector2(work.Right - side, work.Bottom));

        var passthrough = new PassthroughUpdater(GetWindow(), view.Art);
        GetNode<PetController>("Controller").Init(view, mover, passthrough);
    }
}
