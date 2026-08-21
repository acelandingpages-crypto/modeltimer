using System;

namespace ModelTimer;

/// <summary>
/// Single point of access for shift_data.json. Centralizing this (instead of every window
/// building its own path and calling JsonStore directly) means a save from any one window
/// raises <see cref="Changed"/> so every other open window can refresh instead of silently
/// showing stale data until the user remembers to click its own Refresh button.
/// </summary>
internal static class ShiftDataStore
{
    public const int CurrentSchemaVersion = 1;

    public static event Action? Changed;

    public static ShiftDataFile Load()
    {
        var data = JsonStore.Load<ShiftDataFile>(AppPaths.ShiftData) ?? new ShiftDataFile();
        Migrate(data);
        return data;
    }

    public static bool Save(ShiftDataFile data)
    {
        data.SchemaVersion = CurrentSchemaVersion;
        var ok = JsonStore.Save(AppPaths.ShiftData, data);
        if (ok) Changed?.Invoke();
        return ok;
    }

    /// <summary>Upgrades an older on-disk file in place before it's used. A file with
    /// SchemaVersion 0 predates this field entirely (System.Text.Json defaults it to 0) and is
    /// treated as version 1 - no migration needed yet since nothing has changed shape since.</summary>
    private static void Migrate(ShiftDataFile data)
    {
        if (data.SchemaVersion <= 0) data.SchemaVersion = 1;

        // Future migrations go here, e.g.:
        // if (data.SchemaVersion < 2) { ...backfill/rename...; data.SchemaVersion = 2; }
    }

    public static DateTime? GetLatestBackupTime() => JsonStore.GetLatestBackupTime(AppPaths.ShiftData);

    public static bool RestoreLatestBackup()
    {
        var ok = JsonStore.RestoreLatestBackup(AppPaths.ShiftData);
        if (ok) Changed?.Invoke();
        return ok;
    }
}
