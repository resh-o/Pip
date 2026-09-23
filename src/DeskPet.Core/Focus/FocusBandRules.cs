namespace DeskPet.Core.Focus;

/// <summary>
/// Band classification with hysteresis, so a score hovering on a threshold does not make the
/// pet flip moods every sample.
/// </summary>
internal static class FocusBandRules
{
    public const double EnterHigh = 0.70;
    public const double LeaveHigh = 0.65;
    public const double EnterLow = 0.30;
    public const double LeaveLow = 0.35;

    public static FocusBand Next(FocusBand current, double score)
    {
        if (score >= EnterHigh || (current == FocusBand.High && score >= LeaveHigh))
            return FocusBand.High;
        if (score <= EnterLow || (current == FocusBand.Low && score <= LeaveLow))
            return FocusBand.Low;
        return FocusBand.Neutral;
    }
}
