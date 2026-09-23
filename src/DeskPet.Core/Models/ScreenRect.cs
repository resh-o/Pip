namespace DeskPet.Core.Models;

/// <summary>Rectangle in physical screen pixels (right/bottom exclusive).</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public int CenterX => Left + Width / 2;
    public int CenterY => Top + Height / 2;

    public bool Covers(ScreenRect other) =>
        Left <= other.Left && Top <= other.Top && Right >= other.Right && Bottom >= other.Bottom;

    public ScreenRect Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);
}
