using System;
using System.Collections.Generic;

namespace ModelTimer;

public class AppSettings
{
    public string? Theme { get; set; }
    public string? ShowHotkey { get; set; }
    public string? PrivateHotkey { get; set; }
    public string? TypingHotkey { get; set; }
    public bool NotifyOnShiftComplete { get; set; } = true;
    public bool Warn5Min { get; set; } = true;
    public bool Warn15Min { get; set; } = true;
    public string AiProvider { get; set; } = "None";
    public string AiApiKey { get; set; } = string.Empty;
}

public class ShiftDataFile
{
    public List<string>? Models { get; set; }
    public List<string>? Moderators { get; set; }
    public List<ShiftEntry>? Shifts { get; set; }
}

public class ShiftEntry
{
    public string Model { get; set; } = string.Empty;
    public string Moderator { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }
    public int ElapsedHours { get; set; }
    public int ElapsedMinutes { get; set; }
    public int ElapsedSeconds { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? StopTime { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionSummary { get; set; } = string.Empty;
    public string GoodMembers { get; set; } = string.Empty;
    public string IssuesToWatch { get; set; } = string.Empty;
    public int PerformanceRating { get; set; }
}

public class ActiveShiftState
{
    public string Model { get; set; } = string.Empty;
    public string Moderator { get; set; } = string.Empty;
    public long ElapsedSeconds { get; set; }
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }
}

public class CrmDataFile
{
    public List<CrmEntry>? Records { get; set; }
}

public class CrmEntry
{
    public int Id { get; set; }
    public string User { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public string Habits { get; set; } = string.Empty;
    public string Triggers { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int SpendTier { get; set; }
}
