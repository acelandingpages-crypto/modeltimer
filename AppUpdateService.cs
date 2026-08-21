using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ModelTimer;

/// <summary>
/// Checks a public GitHub repo's Releases for a newer packaged build and, if the user agrees,
/// downloads and applies it. Every installed copy (built and published via `vpk`, see
/// PUBLISHING.md) points at the same repo, so publishing a new release there is what makes "do
/// you want to update?" show up everywhere.
///
/// The repo is public specifically so this needs no embedded token: a token scoped read-only to
/// one repo was the original plan, but fine-grained GitHub PATs were unreliable for this repo at
/// setup time, and a public repo with no token at all is strictly safer anyway - there's no
/// secret inside the app for anyone to extract. No user/fan data is ever in this repo either way,
/// only app source and packaged releases.
/// </summary>
internal static class AppUpdateService
{
    private const string RepoUrl = "https://github.com/acelandingpages-crypto/modeltimer";

    public static bool IsConfigured => !RepoUrl.Contains("OWNER/REPO", StringComparison.Ordinal);

    /// <summary>False when running from source (e.g. `dotnet run`) rather than from a copy that
    /// `vpk` installed - there's nothing to update in that case, so update checks are skipped.</summary>
    public static bool IsInstalledCopy => CreateManager()?.IsInstalled ?? false;

    /// <summary>Returns update info if a newer version is available, or null if there's nothing
    /// new, updates aren't configured yet, or this isn't a Velopack-installed copy (e.g. running
    /// from source during development) - callers don't need to check those cases separately.</summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        var manager = CreateManager();
        if (manager == null || !manager.IsInstalled) return null;

        try
        {
            return await manager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            JsonStore.LogError("Update check failed", ex);
            return null;
        }
    }

    /// <summary>Downloads the update and restarts into it. Only returns (without restarting) if
    /// the download itself fails - a successful apply replaces the running process.</summary>
    public static async Task<bool> DownloadAndApplyAsync(UpdateInfo updateInfo, Action<int>? onProgress = null)
    {
        var manager = CreateManager();
        if (manager == null) return false;

        try
        {
            await manager.DownloadUpdatesAsync(updateInfo, onProgress);
            manager.ApplyUpdatesAndRestart(updateInfo);
            return true;
        }
        catch (Exception ex)
        {
            JsonStore.LogError("Update download/apply failed", ex);
            return false;
        }
    }

    public static string CurrentVersion => CreateManager()?.CurrentVersion?.ToString() ?? "dev";

    private static UpdateManager? CreateManager()
    {
        if (!IsConfigured) return null;

        try
        {
            var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
            return new UpdateManager(source);
        }
        catch (Exception ex)
        {
            JsonStore.LogError("Failed to set up update manager", ex);
            return null;
        }
    }
}
