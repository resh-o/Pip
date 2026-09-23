using DeskPet.Core.Abstractions;

namespace DeskPet.Platform;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
