using System;
using System.Linq;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace ModelTimer;

internal static class AiSummaryService
{
    private const string Model = "claude-opus-5";

    public static bool IsConfigured(AppSettings? settings) =>
        settings != null && settings.AiProvider != "None" && !string.IsNullOrWhiteSpace(settings.AiApiKey);

    public static async Task<string> DraftSummaryAsync(AppSettings settings, string model, string moderator, TimeSpan elapsed)
    {
        var client = RequireAnthropicClient(settings);

        var prompt =
            "Write a short, professional shift-report summary (2-4 sentences) for a content-moderation shift. " +
            "Write it in first person, as if the moderator is submitting it themselves. Plain language, no headers, no bullet points.\n\n" +
            $"Model: {model}\nModerator: {moderator}\nTime worked: {elapsed:hh\\:mm\\:ss}";

        return await SendAsync(client, prompt, 512);
    }

    public static async Task<string> AskAsync(AppSettings settings, string question, string databaseJson)
    {
        var client = RequireAnthropicClient(settings);

        var prompt =
            "You are a business analyst for a content-moderation studio. Below is the studio's shift-history and " +
            "fan-CRM data as JSON. Answer the question using only this data - look for trends, totals, top " +
            "performers, and risks, not just a literal lookup. Be concise and concrete, naming specific models, " +
            "moderators, or fans and numbers where relevant. If the data doesn't contain the answer, say so plainly " +
            "rather than guessing.\n\n" +
            $"DATA:\n{databaseJson}\n\nQUESTION: {question}";

        return await SendAsync(client, prompt, 1024);
    }

    private static async Task<string> SendAsync(AnthropicClient client, string prompt, int maxTokens)
    {
        try
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = maxTokens,
                Messages = [new() { Role = Role.User, Content = prompt }]
            });

            return ExtractText(response);
        }
        catch (AnthropicUnauthorizedException)
        {
            throw new InvalidOperationException("The Anthropic API key in Settings was rejected. Double-check it and try again.");
        }
        catch (AnthropicRateLimitException)
        {
            throw new InvalidOperationException("Anthropic rate-limited this request. Wait a moment and try again.");
        }
        catch (AnthropicIOException)
        {
            throw new InvalidOperationException("Couldn't reach the Anthropic API - check your internet connection.");
        }
        catch (AnthropicApiException ex)
        {
            throw new InvalidOperationException($"Anthropic API error: {ex.Message}");
        }
    }

    private static AnthropicClient RequireAnthropicClient(AppSettings settings)
    {
        if (settings.AiProvider != "Anthropic Claude")
        {
            throw new NotSupportedException($"AI features are only wired up for Anthropic Claude right now — set the provider to \"Anthropic Claude\" in Settings (currently \"{settings.AiProvider}\").");
        }
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            throw new NotSupportedException("No API key configured — add one under Settings.");
        }

        return new AnthropicClient { ApiKey = settings.AiApiKey };
    }

    private static string ExtractText(Message response)
    {
        var text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(text) ? "(No response text returned.)" : text.Trim();
    }
}
