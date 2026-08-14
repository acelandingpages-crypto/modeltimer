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

    // Same seam as DraftSummaryAsync: the "Ask" panel already retrieves and displays matching
    // shift/CRM records itself without any AI involved, so this only needs to synthesize a
    // natural-language answer over records the UI has already found.
    public static Task<string> AskAsync(AppSettings settings, string question, string matchingRecordsContext)
    {
        throw new NotSupportedException($"AI-synthesized answers aren't wired up to {settings.AiProvider} yet — this is a placeholder for future integration. The matches below are found by keyword search only.");
    }
}
