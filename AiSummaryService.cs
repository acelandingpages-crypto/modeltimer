using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace ModelTimer;

internal static class AiSummaryService
{
    private const string AnthropicModel = "claude-opus-5";
    private const string OpenRouterModel = "nvidia/nemotron-3-super-120b-a12b:free";
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static bool IsConfigured(AppSettings? settings) =>
        settings != null && settings.AiProvider != "None" && !string.IsNullOrWhiteSpace(settings.AiApiKey);

    public static async Task<string> DraftSummaryAsync(AppSettings settings, string model, string moderator, TimeSpan elapsed)
    {
        var prompt =
            "Write a short, professional shift-report summary (2-4 sentences) for a content-moderation shift. " +
            "Write it in first person, as if the moderator is submitting it themselves. Plain language, no headers, no bullet points.\n\n" +
            $"Model: {model}\nModerator: {moderator}\nTime worked: {elapsed:hh\\:mm\\:ss}";

        return await SendAsync(settings, prompt, 512);
    }

    public static async Task<string> AskAsync(AppSettings settings, string question, string databaseJson)
    {
        var prompt =
            "You are a business analyst for a content-moderation studio. Below is the studio's shift-history and " +
            "fan-CRM data as JSON. Answer the question using only this data - look for trends, totals, top " +
            "performers, and risks, not just a literal lookup. Be concise and concrete, naming specific models, " +
            "moderators, or fans and numbers where relevant. If the data doesn't contain the answer, say so plainly " +
            "rather than guessing.\n\n" +
            $"DATA:\n{databaseJson}\n\nQUESTION: {question}";

        return await SendAsync(settings, prompt, 1024);
    }

    private static Task<string> SendAsync(AppSettings settings, string prompt, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            throw new NotSupportedException("No API key configured — add one under Settings.");
        }

        return settings.AiProvider switch
        {
            "Anthropic Claude" => SendViaAnthropicAsync(settings.AiApiKey, prompt, maxTokens),
            "OpenRouter" => SendViaOpenRouterAsync(settings.AiApiKey, prompt),
            _ => throw new NotSupportedException($"AI features aren't wired up for \"{settings.AiProvider}\" yet — set the provider to \"Anthropic Claude\" or \"OpenRouter\" in Settings.")
        };
    }

    private static async Task<string> SendViaAnthropicAsync(string apiKey, string prompt, int maxTokens)
    {
        var client = new AnthropicClient { ApiKey = apiKey };

        try
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = AnthropicModel,
                MaxTokens = maxTokens,
                Messages = [new() { Role = Role.User, Content = prompt }]
            });

            var text = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(text) ? "(No response text returned.)" : text.Trim();
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

    private static async Task<string> SendViaOpenRouterAsync(string apiKey, string prompt)
    {
        var requestBody = new
        {
            model = OpenRouterModel,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://modeltimer.local");
        request.Headers.Add("X-Title", "ModelTimer");

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException("Couldn't reach OpenRouter - check your internet connection.");
        }

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "The OpenRouter API key in Settings was rejected. Double-check it and try again.",
                System.Net.HttpStatusCode.TooManyRequests => "OpenRouter rate-limited this request (common on free models). Wait a moment and try again.",
                _ => $"OpenRouter API error ({(int)response.StatusCode}): {body}"
            };
            throw new InvalidOperationException(message);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(text) ? "(No response text returned.)" : text.Trim();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new InvalidOperationException($"OpenRouter returned an unexpected response shape: {body}", ex);
        }
    }
}
