using System;
using System.Runtime.Versioning;

namespace ModelTimer;

/// <summary>
/// Single point of access for settings.json. Transparently encrypts/decrypts the AI API key
/// at the disk boundary (see SecretProtector) so every in-memory AppSettings always holds the
/// real key, while the file on disk never does. Also raises <see cref="Changed"/> so open
/// windows can react immediately to a theme or preference change made from Settings.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SettingsStore
{
    public static event Action? Changed;

    public static AppSettings Load()
    {
        var settings = JsonStore.Load<AppSettings>(AppPaths.Settings) ?? new AppSettings();
        settings.AiApiKey = SecretProtector.Unprotect(settings.AiApiKey);
        return settings;
    }

    public static bool Save(AppSettings settings)
    {
        var toWrite = new AppSettings
        {
            Theme = settings.Theme,
            ShowHotkey = settings.ShowHotkey,
            PrivateHotkey = settings.PrivateHotkey,
            TypingHotkey = settings.TypingHotkey,
            NotifyOnShiftComplete = settings.NotifyOnShiftComplete,
            Warn5Min = settings.Warn5Min,
            Warn15Min = settings.Warn15Min,
            AiProvider = settings.AiProvider,
            AiApiKey = SecretProtector.Protect(settings.AiApiKey),
            AiConsentAcknowledged = settings.AiConsentAcknowledged,
            AiIncludeSensitiveNotes = settings.AiIncludeSensitiveNotes
        };

        var ok = JsonStore.Save(AppPaths.Settings, toWrite);
        if (ok) Changed?.Invoke();
        return ok;
    }
}
