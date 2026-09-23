using DeskPet.Core.Abstractions;
using DeskPet.Core.Config;

namespace DeskPet.Config;

/// <summary>Current settings, swapped atomically as a whole immutable record.</summary>
internal sealed class SettingsHolder : ISettings
{
    private Settings _current = new();

    public Settings Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value);
    }
}
