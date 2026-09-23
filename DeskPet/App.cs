using DeskPet.Brain;
using DeskPet.Config;
using DeskPet.Core.Focus;
using DeskPet.Core.Planning;
using DeskPet.Core.Sensing;
using DeskPet.Pet;
using DeskPet.Platform;
using DeskPet.Ui;
using Godot;

namespace DeskPet;

/// <summary>Composition root: sizes and hides the pet window, then wires sensing, planning and the pet together.</summary>
public partial class App : Node
{
    private ForegroundPoller? _poller;
    private FocusCoordinator _coordinator = null!;

    public override void _Ready()
    {
        DiagnosticLog.Start();
        var window = new OwnWindow();
        window.HideFromTaskbar();
        int side = Mathf.RoundToInt(PetMover.CanvasSize.X * window.SystemScale);
        GetWindow().Size = new Vector2I(side, side);

        var view = GetNode<PetView>("Pet");
        var mover = GetNode<PetMover>("Mover");
        var controller = GetNode<PetController>("Controller");
        mover.Init(window);
        var (x, y) = Desktop.CursorPosition();
        var work = Desktop.WorkAreaAt(x, y);
        mover.PlaceAt(new Vector2(work.Right - side, work.Bottom));
        controller.Init(view, mover, new PassthroughUpdater(GetWindow(), view.Art));

        var watch = new OwnWindowWatch();
        AddChild(watch);
        watch.Init(window);

        WireBehaviour(mover, controller);
    }

    public override void _ExitTree() => _poller?.Dispose();

    private void WireBehaviour(PetMover mover, PetController controller)
    {
        var settings = new SettingsHolder();
        var clock = new SystemClock();
        var windows = new Win32WindowService();
        var planner = new ActionPlanner(clock, settings, Path.GetFileNameWithoutExtension(System.Environment.ProcessPath) ?? "DeskPet");

        var dragger = new WindowDragger();
        AddChild(dragger);
        dragger.Init(windows, mover, controller.Machine);

        var runner = new ActionRunner(planner, dragger, controller, windows);
        _coordinator = new FocusCoordinator(planner, new FocusTracker(clock), controller, runner);
        _poller = new ForegroundPoller(windows, new TestMarkerBrain(), new ForegroundDebouncer(clock, settings), settings, Post);
        _poller.Start();
    }

    // Called on the poller thread; Callable.CallDeferred marshals onto the main thread.
    private void Post(Observation observation) =>
        Callable.From(() => _coordinator.OnObservation(observation)).CallDeferred();
}
