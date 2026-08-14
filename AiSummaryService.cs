using System;
using System.Threading.Tasks;

namespace ModelTimer;

internal static class AiSummaryService
{
    public static bool IsConfigured(AppSettings? settings) =>
        settings != null && settings.AiProvider != "None" && !string.IsNullOrWhiteSpace(settings.AiApiKey);

    // Intentionally unimplemented: the settings/UI seam is wired up (provider + API key in
    // Settings, "Draft with AI" button in ShiftReportWindow) so a real provider integration
    // is a self-contained change here once one is chosen.
    public static Task<string> DraftSummaryAsync(AppSettings settings, string model, string moderator, TimeSpan elapsed)
    {
        throw new NotSupportedException($"AI drafting isn't wired up to {settings.AiProvider} yet — this is a placeholder for future integration.");
    }
}
