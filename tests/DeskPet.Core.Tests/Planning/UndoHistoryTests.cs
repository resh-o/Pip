using DeskPet.Core.Models;
using DeskPet.Core.Planning;

namespace DeskPet.Core.Tests.Planning;

public sealed class UndoHistoryTests
{
    private static WindowPlacement Placement(nint handle, int left = 0) =>
        new(handle, new ScreenRect(left, 0, left + 100, 100), WasMaximized: false, WasMinimized: false);

    [Fact]
    public void Empty_history_has_nothing_to_undo()
    {
        Assert.False(new UndoHistory().TryPop(out _));
    }

    [Fact]
    public void Pops_newest_first()
    {
        var history = new UndoHistory();
        history.Push(Placement(1));
        history.Push(Placement(2));

        Assert.True(history.TryPop(out var first));
        Assert.True(history.TryPop(out var second));
        Assert.Equal((nint)2, first.Handle);
        Assert.Equal((nint)1, second.Handle);
        Assert.False(history.TryPop(out _));
    }

    [Fact]
    public void Window_moved_twice_keeps_its_first_original_placement()
    {
        var history = new UndoHistory();
        history.Push(Placement(1, left: 10));
        history.Push(Placement(2));
        history.Push(Placement(1, left: 500));

        Assert.True(history.TryPop(out var top));
        Assert.Equal((nint)1, top.Handle);
        Assert.Equal(10, top.NormalBounds.Left);
        Assert.Equal(2, history.Count + 1);
    }

    [Fact]
    public void Oldest_entries_fall_off_past_capacity()
    {
        var history = new UndoHistory(capacity: 2);
        history.Push(Placement(1));
        history.Push(Placement(2));
        history.Push(Placement(3));

        Assert.Equal(2, history.Count);
        history.TryPop(out _);
        Assert.True(history.TryPop(out var last));
        Assert.Equal((nint)2, last.Handle);
    }

    [Fact]
    public void Forget_removes_only_that_window()
    {
        var history = new UndoHistory();
        history.Push(Placement(1));
        history.Push(Placement(2));

        history.Forget(2);
        history.Forget(99);

        Assert.True(history.TryPop(out var only));
        Assert.Equal((nint)1, only.Handle);
        Assert.Equal(0, history.Count);
    }
}
