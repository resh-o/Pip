using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Core.Brain;

/// <summary>Bounded LRU map from <see cref="TitlePattern"/> keys to verdicts, with optional expiry.</summary>
public sealed class VerdictCache
{
    public const int DefaultCapacity = 512;
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromHours(12);

    private readonly IClock _clock;
    private readonly int _capacity;
    private readonly TimeSpan? _timeToLive;
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _recency = new();

    // The router awaits the API between reads and writes, so callers can overlap.
    private readonly object _gate = new();

    public VerdictCache(IClock clock)
        : this(clock, DefaultCapacity, DefaultTimeToLive)
    {
    }

    /// <param name="timeToLive">Null keeps entries until evicted or reset.</param>
    public VerdictCache(IClock clock, int capacity, TimeSpan? timeToLive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _clock = clock;
        _capacity = capacity;
        _timeToLive = timeToLive;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _index.Count;
            }
        }
    }

    public bool TryGet(string key, out Verdict verdict)
    {
        lock (_gate)
        {
            verdict = default;
            if (!_index.TryGetValue(key, out var node))
            {
                return false;
            }

            if (IsExpired(node.Value))
            {
                Remove(node);
                return false;
            }

            _recency.Remove(node);
            _recency.AddFirst(node);
            verdict = node.Value.Verdict;
            return true;
        }
    }

    public void Set(string key, Verdict verdict)
    {
        lock (_gate)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                Remove(existing);
            }

            _index[key] = _recency.AddFirst(new Entry(key, verdict, _clock.UtcNow));
            if (_index.Count > _capacity)
            {
                Remove(_recency.Last!);
            }
        }
    }

    /// <summary>Forgets everything; verdicts are only meaningful for the goal they were judged against.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _index.Clear();
            _recency.Clear();
        }
    }

    private bool IsExpired(Entry entry) =>
        _timeToLive is { } ttl && _clock.UtcNow - entry.StoredAt >= ttl;

    private void Remove(LinkedListNode<Entry> node)
    {
        _recency.Remove(node);
        _index.Remove(node.Value.Key);
    }

    private sealed record Entry(string Key, Verdict Verdict, DateTimeOffset StoredAt);
}
