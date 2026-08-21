using System;

namespace ModelTimer;

/// <summary>Single point of access for crm_data.json — see ShiftDataStore for why this exists.</summary>
internal static class CrmDataStore
{
    public const int CurrentSchemaVersion = 1;

    public static event Action? Changed;

    public static CrmDataFile Load()
    {
        var data = JsonStore.Load<CrmDataFile>(AppPaths.CrmData) ?? new CrmDataFile();
        Migrate(data);
        return data;
    }

    public static bool Save(CrmDataFile data)
    {
        data.SchemaVersion = CurrentSchemaVersion;
        var ok = JsonStore.Save(AppPaths.CrmData, data);
        if (ok) Changed?.Invoke();
        return ok;
    }

    private static void Migrate(CrmDataFile data)
    {
        if (data.SchemaVersion <= 0) data.SchemaVersion = 1;

        // Future migrations go here.
    }

    public static DateTime? GetLatestBackupTime() => JsonStore.GetLatestBackupTime(AppPaths.CrmData);

    public static bool RestoreLatestBackup()
    {
        var ok = JsonStore.RestoreLatestBackup(AppPaths.CrmData);
        if (ok) Changed?.Invoke();
        return ok;
    }
}
