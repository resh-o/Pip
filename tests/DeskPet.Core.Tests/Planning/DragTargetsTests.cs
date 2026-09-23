using DeskPet.Core.Models;
using DeskPet.Core.Planning;

namespace DeskPet.Core.Tests.Planning;

public sealed class DragTargetsTests
{
    private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);
    private static readonly ScreenRect Window = new(400, 300, 1400, 900); // 1000 x 600

    [Theory]
    [InlineData(ScreenEdge.Left, -800, 300)]
    [InlineData(ScreenEdge.Right, 1720, 300)]
    [InlineData(ScreenEdge.Top, 400, -480)]
    [InlineData(ScreenEdge.Bottom, 400, 960)]
    public void Haul_pushes_most_of_the_window_past_the_edge(ScreenEdge edge, int left, int top)
    {
        var target = DragTargets.HaulDestination(Window, Monitor, edge);

        Assert.Equal(new ScreenRect(left, top, left + 1000, top + 600), target);
    }

    [Fact]
    public void Haul_uses_the_windows_own_monitor_even_with_negative_coordinates()
    {
        var secondary = new ScreenRect(-2560, -360, 0, 1080);
        var window = new ScreenRect(-1000, 100, -200, 700); // 800 wide

        var target = DragTargets.HaulDestination(window, secondary, ScreenEdge.Right);

        Assert.Equal(-160, target.Left); // 640 of 800 px past x = 0
        Assert.Equal(window.Width, target.Width);
    }

    [Fact]
    public void Centre_places_window_in_the_middle_of_the_work_area()
    {
        var work = new ScreenRect(0, 0, 1920, 1040);

        var target = DragTargets.CentreIn(new ScreenRect(10, 10, 810, 510), work);

        Assert.Equal(new ScreenRect(560, 270, 1360, 770), target);
    }

    [Fact]
    public void Centre_keeps_oversized_window_title_bar_on_screen()
    {
        var work = new ScreenRect(-1920, 0, 0, 1040);

        var target = DragTargets.CentreIn(new ScreenRect(0, 0, 2400, 1200), work);

        Assert.Equal(-1920, target.Left);
        Assert.Equal(0, target.Top);
    }

    [Theory]
    [InlineData(ScreenEdge.Left, 70)]
    [InlineData(ScreenEdge.Right, 930)]
    [InlineData(ScreenEdge.Top, 500)]
    [InlineData(null, 500)]
    public void Grip_is_at_the_end_being_pulled_towards(ScreenEdge? edge, int x)
    {
        Assert.Equal((x, 0), DragTargets.GripOffset(Window, edge));
    }

    [Fact]
    public void Grip_inset_shrinks_for_narrow_windows()
    {
        Assert.Equal((50, 0), DragTargets.GripOffset(new ScreenRect(0, 0, 200, 100), ScreenEdge.Left));
    }
}
