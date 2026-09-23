namespace DeskPet.Core.Models;

public sealed record Goal(string Text)
{
    public static readonly Goal None = new(string.Empty);
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}
