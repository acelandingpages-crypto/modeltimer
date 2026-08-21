namespace ModelTimer.Tests;

public class AiSummaryServiceParsingTests
{
    [Fact]
    public void StripJsonFences_RemovesMarkdownCodeFence()
    {
        var raw = "```json\n{\"a\":1}\n```";
        Assert.Equal("{\"a\":1}", AiSummaryService.StripJsonFences(raw));
    }

    [Fact]
    public void StripJsonFences_LeavesPlainJsonUntouched()
    {
        var raw = "{\"a\":1}";
        Assert.Equal(raw, AiSummaryService.StripJsonFences(raw));
    }

    [Fact]
    public void StripJsonFences_TrimsSurroundingWhitespace()
    {
        var raw = "  \n{\"a\":1}\n  ";
        Assert.Equal("{\"a\":1}", AiSummaryService.StripJsonFences(raw));
    }

    [Fact]
    public void ParseAskResult_ParsesWellFormedJson()
    {
        var raw = """
        {
          "headline": "Luna worked the most hours",
          "details": ["Luna: 40h", "Mato: 30h"],
          "chart_title": "Hours by moderator",
          "chart": [{"label": "Luna", "value": 40}, {"label": "Mato", "value": 30}]
        }
        """;

        var result = AiSummaryService.ParseAskResult(raw);

        Assert.Equal("Luna worked the most hours", result.Headline);
        Assert.Equal(2, result.Details.Count);
        Assert.Equal("Hours by moderator", result.ChartTitle);
        Assert.Equal(2, result.Chart.Count);
        Assert.Equal("Luna", result.Chart[0].Label);
        Assert.Equal(40, result.Chart[0].Value);
    }

    [Fact]
    public void ParseAskResult_StripsFencesBeforeParsing()
    {
        var raw = "```json\n{\"headline\":\"ok\",\"details\":[],\"chart_title\":\"\",\"chart\":[]}\n```";

        var result = AiSummaryService.ParseAskResult(raw);

        Assert.Equal("ok", result.Headline);
    }

    [Fact]
    public void ParseAskResult_OnInvalidJson_FallsBackToRawTextAsHeadline()
    {
        var raw = "Sorry, I can't answer that.";

        var result = AiSummaryService.ParseAskResult(raw);

        Assert.Equal(raw, result.Headline);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void CleanMilestoneText_TrimsWhitespaceAndSurroundingQuotes()
    {
        Assert.Equal("Keep going!", AiSummaryService.CleanMilestoneText("  \"Keep going!\"  "));
    }

    [Fact]
    public void CleanMilestoneText_TruncatesAnOverlyLongResponse()
    {
        var raw = new string('x', 200);

        var cleaned = AiSummaryService.CleanMilestoneText(raw);

        Assert.True(cleaned.Length <= 91); // 90 chars + the ellipsis character
        Assert.EndsWith("…", cleaned);
    }

    [Fact]
    public void FormatClock_UnderAnHour_ShowsMinutesOnly()
    {
        Assert.Equal("45m", AiSummaryService.FormatClock(TimeSpan.FromMinutes(45)));
    }

    [Fact]
    public void FormatClock_OverAnHour_ShowsHoursAndMinutes()
    {
        Assert.Equal("2h 15m", AiSummaryService.FormatClock(TimeSpan.FromMinutes(135)));
    }

    [Fact]
    public void ParseAskResult_IgnoresChartPointsMissingARequiredField()
    {
        var raw = """
        {"headline":"x","details":[],"chart_title":"t","chart":[{"label":"onlylabel"},{"label":"y","value":5}]}
        """;

        var result = AiSummaryService.ParseAskResult(raw);

        Assert.Single(result.Chart);
        Assert.Equal("y", result.Chart[0].Label);
    }
}
