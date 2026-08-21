namespace ModelTimer.Tests;

public class ShiftMathTests
{
    [Theory]
    [InlineData(1, 30, 0, 1.5)]
    [InlineData(0, 0, 30, 30.0 / 3600.0)]
    [InlineData(2, 0, 0, 2.0)]
    public void DurationHours_ConvertsComponentsToFractionalHours(int h, int m, int s, double expected)
    {
        Assert.Equal(expected, ShiftMath.DurationHours(h, m, s), precision: 10);
    }

    [Fact]
    public void LostTimeHours_ConvertsSecondsToHours()
    {
        Assert.Equal(0.5, ShiftMath.LostTimeHours(1800), precision: 10);
    }

    [Fact]
    public void PlannedHours_WithNoGoalRecorded_ReturnsMaxValueSoNothingCountsAsOverrun()
    {
        Assert.Equal(double.MaxValue, ShiftMath.PlannedHours(0, 0));
    }

    [Fact]
    public void PlannedHours_CombinesHoursAndMinutes()
    {
        Assert.Equal(2.5, ShiftMath.PlannedHours(2, 30), precision: 10);
    }

    [Fact]
    public void OverrunHours_WhenUnderPlanned_IsZero()
    {
        Assert.Equal(0, ShiftMath.OverrunHours(workedHours: 1.5, plannedHours: 2.0));
    }

    [Fact]
    public void OverrunHours_WhenOverPlanned_ReturnsTheDifference()
    {
        Assert.Equal(0.5, ShiftMath.OverrunHours(workedHours: 2.5, plannedHours: 2.0), precision: 10);
    }

    [Theory]
    [InlineData(0.0, "0 sec")]
    [InlineData(30.0 / 3600.0, "30 sec")]
    [InlineData(1.0 / 60.0, "1 min")]
    [InlineData(1.5, "1 hour 30 min")]
    [InlineData(2.0, "2 hour")]
    public void FormatDuration_ProducesHumanReadableText(double hours, string expected)
    {
        Assert.Equal(expected, ShiftMath.FormatDuration(hours));
    }
}
