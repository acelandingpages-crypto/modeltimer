using System;
using System.IO;

namespace ModelTimer;

internal static class AppPaths
{
    private static string Base => AppDomain.CurrentDomain.BaseDirectory;

    public static string ShiftData => Path.Combine(Base, "shift_data.json");
    public static string CrmData => Path.Combine(Base, "crm_data.json");
    public static string Settings => Path.Combine(Base, "settings.json");
    public static string ActiveShift => Path.Combine(Base, "active_shift.json");
}
