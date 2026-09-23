using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>
/// Decides whether someone other than the pet is moving the window during a drag. Moves are sent
/// asynchronously, so the window may lag a few positions behind; any recently commanded position
/// counts as ours. A size change is the app adjusting to a new monitor DPI, not the user: a user
/// resize shows up as a move/size loop via <c>userHandling</c>.
/// </summary>
public sealed class DragGuard
{
    private const int Capacity = 12;

    private readonly (int Left, int Top)[] _recent = new (int, int)[Capacity];
    private readonly int _tolerance;
    private int _count;
    private int _next;

    public DragGuard(int tolerance = 3) => _tolerance = tolerance;

    public void Begin(ScreenRect start)
    {
        _count = 0;
        _next = 0;
        Commanded(start.Left, start.Top);
    }

    public void Commanded(int left, int top)
    {
        _recent[_next] = (left, top);
        _next = (_next + 1) % Capacity;
        _count = Math.Min(_count + 1, Capacity);
    }

    public bool IsDisturbed(ScreenRect actual, bool userHandling) =>
        userHandling || !IsNearRecent(actual.Left, actual.Top);

    private bool IsNearRecent(int left, int top)
    {
        for (int i = 0; i < _count; i++)
        {
            var (l, t) = _recent[i];
            if (Math.Abs(l - left) <= _tolerance && Math.Abs(t - top) <= _tolerance)
                return true;
        }
        return false;
    }
}
