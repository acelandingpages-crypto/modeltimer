namespace ModelTimer;

/// <summary>
/// Pure duration/hours math shared by the Activity dashboard and shift history. Pulled out of
/// ActivityWindow so it can be unit tested without spinning up an Avalonia window.
/// </summary>
internal static class ShiftMath
{
    public static double DurationHours(int elapsedHours, int elapsedMinutes, int elapsedSeconds) =>
        elapsedHours + elapsedMinutes / 60.0 + elapsedSeconds / 3600.0;

    public static double LostTimeHours(int lostTimeSeconds) => lostTimeSeconds / 3600.0;

    /// <summary>The goal duration for a shift, in hours. Returns double.MaxValue when no goal
    /// was recorded (0h0m) so nothing is ever misclassified as "overrun" against a blank goal.</summary>
    public static double PlannedHours(int durationHours, int durationMinutes)
    {
        var planned = durationHours + durationMinutes / 60.0;
        return planned > 0 ? planned : double.MaxValue;
    }

    public static double OverrunHours(double workedHours, double plannedHours) =>
        System.Math.Max(0, workedHours - plannedHours);

    public static string FormatDuration(double totalHours)
    {
        var totalSeconds = (int)System.Math.Round(totalHours * 3600);
        if (totalSeconds <= 0) return "0 sec";

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            if (minutes > 0 && seconds > 0)
                return $"{hours} hour {minutes} min {seconds} sec";
            if (minutes > 0)
                return $"{hours} hour {minutes} min";
            return $"{hours} hour";
        }

        if (minutes > 0)
        {
            if (seconds > 0)
                return $"{minutes} min {seconds} sec";
            return $"{minutes} min";
        }

        return $"{seconds} sec";
    }
}
