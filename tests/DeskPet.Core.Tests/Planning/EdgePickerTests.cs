using DeskPet.Core.Models;
using DeskPet.Core.Planning;

namespace DeskPet.Core.Tests.Planning;

public sealed class EdgePickerTests
{
    private static readonly ScreenRect Primary = new(0, 0, 1920, 1080);

    // Secondary monitor to the left and above the primary: all coordinates negative.
    private static readonly ScreenRect LeftMonitor = new(-2560, -360, 0, 1080);

    [Theory]
    [InlineData(100, 400, 500, 700, ScreenEdge.Left)]
    [InlineData(1500, 400, 1900, 700, ScreenEdge.Right)]
    [InlineData(700, 0, 1200, 200, ScreenEdge.Top)]
    [InlineData(700, 900, 1200, 1080, ScreenEdge.Bottom)]
    public void Picks_edge_nearest_to_window_centre(int left, int top, int right, int bottom, ScreenEdge expected)
    {
        Assert.Equal(expected, EdgePicker.Nearest(new ScreenRect(left, top, right, bottom), Primary));
    }

    [Theory]
    [InlineData(-2500, 100, -2000, 600, ScreenEdge.Left)]
    [InlineData(-600, 100, -100, 600, ScreenEdge.Right)]
    [InlineData(-1500, -350, -1000, -150, ScreenEdge.Top)]
    [InlineData(-1500, 900, -1000, 1080, ScreenEdge.Bottom)]
    public void Works_with_negative_multi_monitor_coordinates(int left, int top, int right, int bottom, ScreenEdge expected)
    {
        Assert.Equal(expected, EdgePicker.Nearest(new ScreenRect(left, top, right, bottom), LeftMonitor));
    }

    [Fact]
    public void Ties_prefer_horizontal_edges()
    {
        var centred = new ScreenRect(0, 0, 1080, 1080);

        Assert.Equal(ScreenEdge.Left, EdgePicker.Nearest(centred, new ScreenRect(0, 0, 1080, 1080)));
    }

    [Fact]
    public void Window_centre_off_monitor_picks_the_side_it_overhangs()
    {
        var overhangingRight = new ScreenRect(1800, 400, 2400, 700);

        Assert.Equal(ScreenEdge.Right, EdgePicker.Nearest(overhangingRight, Primary));
    }
}
