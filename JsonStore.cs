using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModelTimer;

internal static class JsonStore
{
    private static readonly object LogLock = new();
    private const int MaxBackups = 5;

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

    /// <summary>
    /// Writes via a temp file + atomic replace so a crash or power loss mid-write can never
    /// leave the real file truncated or invalid, and keeps a rolling set of backups of what
    /// was there before so a bad write (or a bad edit) is always recoverable.
    /// </summary>
    public static bool Save<T>(string path, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                BackupExisting(path);
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to save {path}", ex);
            return false;
        }
    }

    /// <summary>Restores the most recent backup over the live file, backing up the current
    /// (possibly corrupt) file first so a restore is itself never a one-way trip.</summary>
    public static bool RestoreLatestBackup(string path)
    {
        try
        {
            var latest = GetBackups(path).FirstOrDefault();
            if (latest == null) return false;

            if (File.Exists(path))
            {
                BackupExisting(path);
            }

            File.Copy(latest, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to restore backup for {path}", ex);
            return false;
        }
    }

    public static DateTime? GetLatestBackupTime(string path)
    {
        var latest = GetBackups(path).FirstOrDefault();
        if (latest == null) return null;
        try
        {
            return File.GetLastWriteTime(latest);
        }
        catch
        {
            return null;
        }
    }

    private static string[] GetBackups(string path)
    {
        var dir = BackupDirFor(path);
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Directory.GetFiles(dir, $"{name}_*{ext}")
            .OrderByDescending(f => f)
            .ToArray();
    }

    private static string BackupDirFor(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? ".", "backups");

    private static void BackupExisting(string path)
    {
        try
        {
            var dir = BackupDirFor(path);
            Directory.CreateDirectory(dir);

            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var backupPath = Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}");
            File.Copy(path, backupPath, overwrite: true);

            var stale = GetBackups(path).Skip(MaxBackups);
            foreach (var old in stale)
            {
                File.Delete(old);
            }
        }
        catch (Exception ex)
        {
            // A failed backup must never block the actual save.
            LogError($"Failed to back up {path}", ex);
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
