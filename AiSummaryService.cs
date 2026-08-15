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

internal class AskResult
{
    public string Headline { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
    public string ChartTitle { get; set; } = string.Empty;
    public List<AskChartPoint> Chart { get; set; } = new();
}

internal class AskChartPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

internal static class AiSummaryService
{
    private const string AnthropicModel = "claude-opus-5";
    private const string OpenRouterModel = "nvidia/nemotron-3-super-120b-a12b:free";
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

    private const string AskJsonSchema = """
    {
      "type": "object",
      "properties": {
        "headline": { "type": "string" },
        "details": { "type": "array", "items": { "type": "string" } },
        "chart_title": { "type": "string" },
        "chart": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "label": { "type": "string" },
              "value": { "type": "number" }
            },
            "required": ["label", "value"],
            "additionalProperties": false
          }
        }
      },
      "required": ["headline", "details", "chart_title", "chart"],
      "additionalProperties": false
    }
    """;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static bool IsConfigured(AppSettings? settings) =>
        settings != null && settings.AiProvider != "None" && !string.IsNullOrWhiteSpace(settings.AiApiKey);

    public static async Task<string> PolishSummaryAsync(AppSettings settings, string model, string moderator, TimeSpan elapsed, string moderatorNotes)
    {
        var prompt =
            "You are helping a content-moderation studio moderator turn their own shift notes into a handoff " +
            "note for whoever works this model next. Rewrite the RAW NOTES below into a single natural, " +
            "well-organized paragraph - no labels, no headers, no bullet points. A good handoff paragraph reads " +
            "smoothly but still makes it easy to pick out three things at a glance: how the shift/model went " +
            "overall, anything notable worth flagging, and anything the next moderator specifically needs to " +
            "watch for or do. Only cover what the raw notes actually contain - do not invent facts, and do not " +
            "force a mention of something the notes never touched on just to fill out the shape. Preserve every " +
            "fact, name, and detail exactly as given. If the notes are brief, the paragraph should stay brief " +
            "too - do not pad it with filler. First person, 2-5 sentences, no signature line.\n\n" +
            $"Context (for tone only, not to be added as new facts): Model: {model} | Moderator: {moderator} | Time worked: {elapsed:hh\\:mm\\:ss}\n\n" +
            $"RAW NOTES:\n{moderatorNotes}";

        return await SendAsync(settings, prompt, 512);
    }

    public static async Task<AskResult> AskAsync(AppSettings settings, string question, string databaseJson)
    {
        var prompt =
            "You are a business analyst for a content-moderation studio. Below is the studio's shift-history and " +
            "fan-CRM data. Answer the question using only this data - look for trends, totals, top performers, " +
            "and risks, not just a literal lookup. Respond with ONLY a JSON object (no markdown fences, no text " +
            "outside the JSON) with this exact shape:\n" +
            "{\n" +
            "  \"headline\": \"one direct sentence answering the question\",\n" +
            "  \"details\": [\"2-5 short, concrete supporting sentences, naming specific models/moderators/fans and numbers\"],\n" +
            "  \"chart_title\": \"short chart title, or an empty string if a chart wouldn't help answer this question\",\n" +
            "  \"chart\": [{\"label\": \"category name\", \"value\": 0}]\n" +
            "}\n" +
            "Only fill \"chart\" when the question compares a small number of named categories by a number (e.g. " +
            "hours by model, count by moderator, spend tier by fan) - leave it an empty array otherwise. Never " +
            "invent numbers, names, or a chart the data doesn't actually support.\n\n" +
            $"DATA:\n{databaseJson}\n\nQUESTION: {question}";

        var raw = await SendJsonAsync(settings, prompt, 1500);
        return ParseAskResult(raw);
    }

    private static AskResult ParseAskResult(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0) cleaned = cleaned[(firstNewline + 1)..];
            var fenceEnd = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0) cleaned = cleaned[..fenceEnd];
            cleaned = cleaned.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var result = new AskResult
            {
                Headline = root.TryGetProperty("headline", out var h) ? h.GetString() ?? string.Empty : string.Empty,
                ChartTitle = root.TryGetProperty("chart_title", out var ct) ? ct.GetString() ?? string.Empty : string.Empty
            };

            if (root.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in d.EnumerateArray())
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) result.Details.Add(text!);
                }
            }

            if (root.TryGetProperty("chart", out var c) && c.ValueKind == JsonValueKind.Array)
            {
                foreach (var point in c.EnumerateArray())
                {
                    if (point.TryGetProperty("label", out var lbl) && point.TryGetProperty("value", out var val) &&
                        val.ValueKind is JsonValueKind.Number)
                    {
                        result.Chart.Add(new AskChartPoint { Label = lbl.GetString() ?? string.Empty, Value = val.GetDouble() });
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(result.Headline) && result.Details.Count == 0)
            {
                result.Headline = raw.Trim();
            }

            return result;
        }
        catch (JsonException)
        {
            return new AskResult { Headline = raw.Trim() };
        }
    }

    private static Task<string> SendAsync(AppSettings settings, string prompt, int maxTokens)
    {
        var apiKey = RequireApiKey(settings);
        return settings.AiProvider switch
        {
            "Anthropic Claude" => SendViaAnthropicAsync(apiKey, prompt, maxTokens),
            "OpenRouter" => SendViaOpenRouterAsync(apiKey, prompt, jsonMode: false),
            _ => throw NotSupportedProvider(settings.AiProvider)
        };
    }

    private static Task<string> SendJsonAsync(AppSettings settings, string prompt, int maxTokens)
    {
        var apiKey = RequireApiKey(settings);
        return settings.AiProvider switch
        {
            "Anthropic Claude" => SendViaAnthropicJsonAsync(apiKey, prompt, maxTokens),
            "OpenRouter" => SendViaOpenRouterAsync(apiKey, prompt, jsonMode: true),
            _ => throw NotSupportedProvider(settings.AiProvider)
        };
    }

    private static string RequireApiKey(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            throw new NotSupportedException("No API key configured — add one under Settings.");
        }
        return settings.AiApiKey;
    }

    private static NotSupportedException NotSupportedProvider(string provider) =>
        new($"AI features aren't wired up for \"{provider}\" yet — set the provider to \"Anthropic Claude\" or \"OpenRouter\" in Settings.");

    private static Task<string> SendViaAnthropicAsync(string apiKey, string prompt, int maxTokens) =>
        InvokeAnthropicAsync(apiKey, new MessageCreateParams
        {
            Model = AnthropicModel,
            MaxTokens = maxTokens,
            Messages = [new() { Role = Role.User, Content = prompt }]
        });

    private static Task<string> SendViaAnthropicJsonAsync(string apiKey, string prompt, int maxTokens) =>
        InvokeAnthropicAsync(apiKey, new MessageCreateParams
        {
            Model = AnthropicModel,
            MaxTokens = maxTokens,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(AskJsonSchema)!
                }
            },
            Messages = [new() { Role = Role.User, Content = prompt }]
        });

    private static async Task<string> InvokeAnthropicAsync(string apiKey, MessageCreateParams parameters)
    {
        var client = new AnthropicClient { ApiKey = apiKey };

        try
        {
            var response = await client.Messages.Create(parameters);

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

    private static async Task<string> SendViaOpenRouterAsync(string apiKey, string prompt, bool jsonMode)
    {
        object requestBody = jsonMode
            ? new
            {
                model = OpenRouterModel,
                response_format = new { type = "json_object" },
                messages = new[] { new { role = "user", content = prompt } }
            }
            : new
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
