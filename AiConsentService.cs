using Avalonia.Controls;
using System.Threading.Tasks;

namespace ModelTimer;

/// <summary>
/// Every AI feature sends shift notes and/or fan CRM data (which can include usernames, spend
/// tier, habits, and "triggers") to a third-party provider over the internet. Nothing in the app
/// disclosed that before this existed. Any UI entry point that triggers an AI call must await
/// this first and bail out if it returns false.
/// </summary>
internal static class AiConsentService
{
    public static async Task<bool> EnsureConsentAsync(Window owner, AppSettings settings)
    {
        if (settings.AiConsentAcknowledged) return true;

        var message =
            "Using an AI feature sends the relevant shift notes and/or fan (CRM) data - which can " +
            "include usernames, spend tier, habits, and \"triggers\" - to the AI provider configured " +
            "in Settings, over the internet. This data leaves this machine.\n\n" +
            "You can stop sensitive notes specifically from being included at any time, from " +
            "Settings → AI → \"Include sensitive notes in AI requests.\"\n\n" +
            "Continue?";

        var confirmed = await AppDialog.ShowConfirm(owner, "Before You Use AI Features", message, confirmLabel: "Continue");
        if (!confirmed) return false;

        settings.AiConsentAcknowledged = true;
        SettingsStore.Save(settings);
        return true;
    }
}
