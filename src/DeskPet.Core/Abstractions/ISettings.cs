using DeskPet.Core.Config;

namespace DeskPet.Core.Abstractions;

public interface ISettings
{
    Settings Current { get; }
}
