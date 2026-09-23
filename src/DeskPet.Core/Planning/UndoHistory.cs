using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>Original placements of windows the pet moved, newest first, bounded.</summary>
public sealed class UndoHistory
{
    private readonly int _capacity;
    private readonly LinkedList<WindowPlacement> _entries = new();

    public UndoHistory(int capacity = 10) => _capacity = Math.Max(1, capacity);

    public int Count => _entries.Count;

    /// <summary>Records a window's original placement; a window moved twice keeps its first original.</summary>
    public void Push(WindowPlacement original)
    {
        var existing = Find(original.Handle);
        if (existing is not null)
        {
            _entries.Remove(existing);
            _entries.AddFirst(existing);
            return;
        }
        _entries.AddFirst(original);
        if (_entries.Count > _capacity)
            _entries.RemoveLast();
    }

    public bool TryPop(out WindowPlacement placement)
    {
        if (_entries.First is null)
        {
            placement = null!;
            return false;
        }
        placement = _entries.First.Value;
        _entries.RemoveFirst();
        return true;
    }

    /// <summary>Drops the entry for a window, e.g. after the user took it back mid-drag.</summary>
    public void Forget(nint handle)
    {
        var node = Find(handle);
        if (node is not null)
            _entries.Remove(node);
    }

    private LinkedListNode<WindowPlacement>? Find(nint handle)
    {
        for (var node = _entries.First; node is not null; node = node.Next)
            if (node.Value.Handle == handle)
                return node;
        return null;
    }
}
