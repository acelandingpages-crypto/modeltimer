using System;

namespace ModelTimer;

public class ShiftHistoryItem
{
    public int Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Moderator { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? StopTime { get; set; }
    public DateTime Timestamp { get; set; }
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }
    public int ElapsedHours { get; set; }
    public int ElapsedMinutes { get; set; }
    public int ElapsedSeconds { get; set; }

    public string Date => Timestamp.ToString("yyyy-MM-dd");
    public string StartTimeDisplay => StartTime.ToString("HH:mm");
    public string StopTimeDisplay
    {
        get
        {
            if (!StopTime.HasValue) return "-";
            if (StopTime.Value.Date > StartTime.Date)
                return StopTime.Value.ToString("yyyy-MM-dd HH:mm");
            return StopTime.Value.ToString("HH:mm");
        }
    }
    public string DurationDisplay
    {
        get
        {
            var totalMinutes = ElapsedHours * 60 + ElapsedMinutes;
            if (totalMinutes == 0 && ElapsedSeconds == 0) return "0 minutes";

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0)
            {
                if (minutes > 0 && ElapsedSeconds > 0)
                    return $"{hours}h {minutes}min {ElapsedSeconds}s";
                else if (minutes > 0)
                    return $"{hours}h {minutes}min";
                else if (ElapsedSeconds > 0)
                    return $"{hours}h {ElapsedSeconds}s";
                return $"{hours}h";
            }

            if (minutes > 0 && ElapsedSeconds > 0)
                return $"{minutes}min {ElapsedSeconds}s";
            else if (minutes > 0)
                return $"{minutes} minutes";
            
            return $"{ElapsedSeconds} seconds";
        }
    }
    public int No => Id;
}
