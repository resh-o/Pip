using DeskPet.Core.Models;
using DeskPet.Core.Planning;

namespace DeskPet.Core.Tests.Planning;

public sealed class DragGuardTests
{
    private readonly DragGuard _guard = new(tolerance: 3);

    private static ScreenRect At(int left, int top, int width = 800, int height = 600) =>
        new(left, top, left + width, top + height);

    [Fact]
    public void Window_where_we_put_it_is_not_disturbed()
    {
        _guard.Begin(At(100, 100));
        _guard.Commanded(120, 100);

        Assert.False(_guard.IsDisturbed(At(120, 100), userHandling: false));
    }

    [Fact]
    public void Lagging_behind_our_async_moves_is_fine()
    {
        _guard.Begin(At(100, 100));
        for (int x = 110; x <= 200; x += 10)
            _guard.Commanded(x, 100);

        Assert.False(_guard.IsDisturbed(At(150, 100), userHandling: false));
        Assert.False(_guard.IsDisturbed(At(100, 100), userHandling: false));
    }

    [Fact]
    public void Small_rounding_differences_are_tolerated()
    {
        _guard.Begin(At(100, 100));

        Assert.False(_guard.IsDisturbed(At(103, 98), userHandling: false));
        Assert.True(_guard.IsDisturbed(At(104, 100), userHandling: false));
    }

    [Fact]
    public void Moved_somewhere_we_never_sent_it_is_disturbed()
    {
        _guard.Begin(At(100, 100));
        _guard.Commanded(120, 100);

        Assert.True(_guard.IsDisturbed(At(500, 300), userHandling: false));
    }

    [Fact]
    public void Positions_older_than_the_window_no_longer_count()
    {
        _guard.Begin(At(0, 0));
        for (int i = 1; i <= 12; i++)
            _guard.Commanded(i * 10, 0);

        Assert.True(_guard.IsDisturbed(At(0, 0), userHandling: false));
        Assert.False(_guard.IsDisturbed(At(30, 0), userHandling: false));
    }

    [Fact]
    public void User_handling_always_disturbs_even_in_place()
    {
        _guard.Begin(At(100, 100));

        Assert.True(_guard.IsDisturbed(At(100, 100), userHandling: true));
    }

    [Fact]
    public void Size_change_at_a_commanded_position_is_a_dpi_adjustment_not_the_user()
    {
        _guard.Begin(At(100, 100));
        _guard.Commanded(200, 100);

        Assert.False(_guard.IsDisturbed(At(200, 100, 600, 400), userHandling: false));
    }

    [Fact]
    public void Begin_forgets_the_previous_drag()
    {
        _guard.Begin(At(100, 100));
        _guard.Begin(At(900, 900));

        Assert.True(_guard.IsDisturbed(At(100, 100), userHandling: false));
    }
}
