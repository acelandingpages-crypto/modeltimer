using System;
using System.IO;
using System.Text.Json;

namespace ModelTimer;

internal static class JsonStore
{
    private static readonly object LogLock = new();

    public static T? Load<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            LogError($"Failed to load {path}", ex);
            return null;
        }
    }

    public static bool Save<T>(string path, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to save {path}", ex);
            return false;
        }
    }

    public static void LogError(string context, Exception ex)
    {
        try
        {
            lock (LogLock)
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never itself throw and take down the caller.
        }
    }
}
