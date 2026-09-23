namespace DeskPet.Core.Sensing;

/// <summary>What the controller should do after a foreground poll. Flags because both can be due at once.</summary>
[Flags]
public enum DebounceResult
{
    None = 0,
    Judge = 1,
    Enumerate = 2,
}
