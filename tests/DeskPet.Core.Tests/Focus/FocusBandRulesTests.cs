using DeskPet.Core.Focus;

namespace DeskPet.Core.Tests.Focus;

public sealed class FocusBandRulesTests
{
    [Theory]
    [InlineData(FocusBand.Neutral, 0.69, FocusBand.Neutral)]
    [InlineData(FocusBand.Neutral, 0.70, FocusBand.High)]
    [InlineData(FocusBand.High, 0.66, FocusBand.High)]
    [InlineData(FocusBand.High, 0.64, FocusBand.Neutral)]
    [InlineData(FocusBand.Neutral, 0.31, FocusBand.Neutral)]
    [InlineData(FocusBand.Neutral, 0.30, FocusBand.Low)]
    [InlineData(FocusBand.Low, 0.34, FocusBand.Low)]
    [InlineData(FocusBand.Low, 0.36, FocusBand.Neutral)]
    [InlineData(FocusBand.Low, 0.80, FocusBand.High)]
    [InlineData(FocusBand.High, 0.10, FocusBand.Low)]
    public void Applies_thresholds_with_hysteresis(FocusBand current, double score, FocusBand expected)
    {
        Assert.Equal(expected, FocusBandRules.Next(current, score));
    }
}
