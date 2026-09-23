using DeskPet.Core.Abstractions;
using DeskPet.Core.Config;

namespace DeskPet.Core.Tests.Fakes;

public sealed class TestSettings : ISettings
{
    public TestSettings(Settings? settings = null) => Current = settings ?? new Settings();

    public Settings Current { get; set; }
}
